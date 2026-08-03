using System.Globalization;
using System.Text.Json;

namespace WeakKeyScanner;

/// <summary>
/// One compromise event in the ECONOMICS.md schema — the bridge from an on-chain
/// finding to an economic claim. The BTC-denominated and classification fields are
/// computable now from a <see cref="Finding"/>; the USD-at-time and precise
/// sweep-latency fields require per-transaction timestamps and a block-granular
/// price series, so they are null until the ever-funded index lands. They are
/// declared here anyway so the schema is stable across that transition.
/// </summary>
public sealed record EconEvent(
    string PatternId,
    string Address,
    string Classification,     // victim | deliberate-published | deliberate-tip | deliberate-dust | ambiguous
    string ClassReason,
    int FundingTxs,
    int SweepTxs,
    decimal DepositValueBtc,
    decimal SweptValueBtc,
    long BalanceSats,
    int FunderCount,
    int SweeperCount,
    string? FirstSeen,
    string? LastSeen,
    int? ActiveWindowDays,
    // Node-gated: require per-tx timestamps + a price series. Null until then.
    decimal? DepositValueUsdAtDeposit,
    decimal? SweptValueUsdAtSweep,
    double? SweepLatencySeconds);

/// <summary>
/// Turns raw findings into classified compromise events per ECONOMICS.md. The
/// classifier thresholds are pre-registered there and fixed here; keep the two in
/// sync. The published-key denylist seeded below is also the germ of the
/// poisoned-address list the defense ships.
/// </summary>
public static class Econ
{
    // Pre-registered thresholds (ECONOMICS.md). Changing these is a methodology
    // change and must be recorded in git history with its motivation.
    public const long DustSats = 10_000;            // 0.0001 BTC single-deposit floor
    public const int TipInboundMin = 10;            // "many" small deposits ...
    public const long TipAvgDepositSats = 200_000;  // ... each small (~0.002 BTC)

    /// <summary>Known-published weak keys/addresses: excluded as deliberate, never victims.</summary>
    public static (HashSet<string> Keys, HashSet<string> Addrs) SeedDenylist()
    {
        var keys = new HashSet<string>(StringComparer.Ordinal)
        {
            "correct horse battery staple", "password", "123456", "bitcoin", "satoshi",
            "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about",
        };
        var addrs = new HashSet<string>(StringComparer.Ordinal)
        {
            "1EHNa6Q4Jz2uvNExL497mE43ikXhwF6kZm", // privkey = 1, uncompressed
            "1BgGZ9tcN4rm9KBzDn7KprQz87SZ26SAMH", // privkey = 1, compressed
        };
        return (keys, addrs);
    }

    public static (string Classification, string Reason) Classify(
        Finding f, ISet<string> publishedKeys, ISet<string> publishedAddrs)
    {
        if (publishedAddrs.Contains(f.Address) || publishedKeys.Contains(f.WeakKey))
            return ("deliberate-published", "known-published weak key/address");

        long received = f.TotalReceivedSats;
        int inbound = f.InboundTxs;

        if (inbound <= 1 && received <= DustSats)
            return ("deliberate-dust", $"single deposit <= {Format.Btc(DustSats)}");

        long avg = inbound > 0 ? received / inbound : received;
        if (inbound >= TipInboundMin && avg <= TipAvgDepositSats && f.Funders.Length >= TipInboundMin / 2)
            return ("deliberate-tip", $"{inbound} deposits, avg {Format.Btc(avg)}, {f.Funders.Length} funders");

        bool swept = f.OutboundTxs > 0;
        if (received > DustSats && swept)
            return ("victim", "non-published, funded above dust, swept");

        return ("ambiguous", "matches neither victim nor deliberate rule");
    }

    public static EconEvent ToEvent(Finding f, ISet<string> keys, ISet<string> addrs)
    {
        var (cls, reason) = Classify(f, keys, addrs);
        long spent = f.TotalReceivedSats - f.BalanceSats; // received - balance = swept out
        return new EconEvent(
            PatternId: f.PatternType,
            Address: f.Address,
            Classification: cls,
            ClassReason: reason,
            FundingTxs: f.InboundTxs,
            SweepTxs: f.OutboundTxs,
            DepositValueBtc: f.TotalReceivedSats / 100_000_000m,
            SweptValueBtc: spent / 100_000_000m,
            BalanceSats: f.BalanceSats,
            FunderCount: f.Funders.Length,
            SweeperCount: f.Sweepers.Length,
            FirstSeen: f.FirstSeen,
            LastSeen: f.LastSeen,
            ActiveWindowDays: WindowDays(f.FirstSeen, f.LastSeen),
            DepositValueUsdAtDeposit: null,   // node-gated
            SweptValueUsdAtSweep: null,       // node-gated
            SweepLatencySeconds: null);       // node-gated
    }

    private static int? WindowDays(string? first, string? last)
    {
        if (!TryDate(first, out var a) || !TryDate(last, out var b)) return null;
        return (int)Math.Round((b - a).TotalDays);
    }

    private static bool TryDate(string? s, out DateTime d) =>
        DateTime.TryParseExact(s, "yyyy-MM-dd", CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AdjustToUniversal, out d);

    /// <summary>Writes one EconEvent JSON object per line.</summary>
    public static void WriteEvents(IEnumerable<EconEvent> events, string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        using var w = new StreamWriter(full, append: false);
        var opts = new JsonSerializerOptions { WriteIndented = false };
        foreach (var e in events) w.WriteLine(JsonSerializer.Serialize(e, opts));
    }
}
