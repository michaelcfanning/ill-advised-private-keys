using System.Globalization;
using System.Text.Json;

namespace WeakKeyScanner;

/// <summary>
/// One compromised-key record: the weak key, where it derives to, the on-chain
/// funding facts, the funder/sweeper sets, the activity window, and a public link.
/// </summary>
public sealed record Finding(
    string PatternType,
    string WeakKey,
    string Kind,
    string Path,
    string Chain,
    string Address,
    bool EverFunded,
    int TxCount,
    long TotalReceivedSats,
    long BalanceSats,
    int InboundTxs,
    int OutboundTxs,
    string? FirstSeen,
    string? LastSeen,
    string[] Funders,
    string[] Sweepers,
    string ExplorerUrl)
{
    public static Finding From(string patternType, string weakKey, DerivedAddress d,
                               AddressActivity a) => new(
        patternType, weakKey, d.Kind, d.Path, "bitcoin", d.Address,
        a.EverFunded, a.TxCount, a.TotalReceivedSats, a.BalanceSats,
        a.InboundTxs, a.OutboundTxs,
        a.FirstSeen?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        a.LastSeen?.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
        a.Funders.ToArray(), a.Sweepers.ToArray(),
        Explorers.Address(d.Address));
}

public static class Explorers
{
    public static string Address(string address) => $"https://mempool.space/address/{address}";
}

public static class Format
{
    public static string Btc(long sats) =>
        (sats / 100_000_000m).ToString("0.########", CultureInfo.InvariantCulture) + " BTC";

    public static string Activity(FundingInfo f)
    {
        if (!f.EverFunded) return "no on-chain history";
        var note = f.BalanceSats > 0 ? "LIVE BALANCE" : "funded then drained";
        return $"{f.TxCount} txs · received {Btc(f.TotalReceivedSats)} · balance {Btc(f.BalanceSats)} · {note}";
    }

    public static string Activity(AddressActivity a)
    {
        if (!a.EverFunded) return "no on-chain history";
        var note = a.BalanceSats > 0 ? "LIVE BALANCE" : "funded then drained";
        var span = a.FirstSeen is null ? "" :
            $" · {a.FirstSeen:yyyy-MM-dd}→{a.LastSeen:yyyy-MM-dd}";
        return $"{a.TxCount} txs ({a.InboundTxs} in / {a.OutboundTxs} out) · received {Btc(a.TotalReceivedSats)} · " +
               $"balance {Btc(a.BalanceSats)} · {a.Funders.Count} funders · {a.Sweepers.Count} sweepers · {note}{span}";
    }
}

/// <summary>Appends findings to a local JSONL file (one JSON object per line).</summary>
public sealed class FindingsWriter : IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private readonly StreamWriter _writer;
    public string Path { get; }

    public FindingsWriter(string path, bool append = false)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer = new StreamWriter(Path, append) { AutoFlush = true };
    }

    public void Write(Finding f) => _writer.WriteLine(JsonSerializer.Serialize(f, Options));

    public void Dispose() => _writer.Dispose();
}
