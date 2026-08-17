namespace WeakKeyScanner;

/// <summary>
/// Classifies each funded weak address by whether its funds were <b>raced</b> (an attacker
/// sweep) or moved on the owner's own schedule (custody), from two on-chain signals plus
/// flow shape. The point is to keep self-custody value out of "loss": most funded weak
/// addresses in our data are owners knowingly using weak/vanity keys, not theft victims.
///
///   * <b>Sweep latency</b> (funding → first spend): an attacker drain is a fee-bidding
///     race, so its latency is ~0. Any real latency means nobody was racing → custody.
///     This is the primary axis.
///   * <b>Sweeper reach</b>: a <i>drainer</i> destination consolidates ≥ <see cref="DrainerMinReach"/>
///     distinct weak addresses (a serial collector); a single-use destination does not.
///   * <b>Flow shape</b>: an address re-funded (≥ <see cref="ActiveInbound"/> inbound) or
///     held (≥ <see cref="ActiveHoldDays"/> days) is a working wallet even when some of its
///     spends are same-day — so latency alone must not condemn an active wallet (e.g. the
///     0xFACED address: re-funded four times with change cycling back, yet same-day spends).
///
/// Buckets, applied in precedence order:
///   <see cref="Bucket.Live"/>          — never spent.
///   <see cref="Bucket.CustodyLatency"/> — spent, but no spend was a race → owner-moved.
///   <see cref="Bucket.Drain"/>         — raced AND a serial-collector destination → harvested.
///   <see cref="Bucket.CustodyActive"/> — raced but re-funded/held via a single-use dest → working wallet.
///   <see cref="Bucket.OneShot"/>       — raced, one deposit, single-use dest → self-test OR quiet drain.
///
/// Intent is not chain-visible. <see cref="Bucket.Drain"/> is a consolidation <i>mechanism</i>
/// (a malicious bot and a prior researcher's sweep tool look identical); <see cref="Bucket.OneShot"/>
/// merges self-test and quiet drain — separating them needs funder resolution (funder == sweeper
/// ⇒ self-test), which the node walk currently defers.
/// </summary>
public static class Classify
{
    public const int RacedMaxDays = 1;    // fund→spend ≤ this many days counts as "raced"
    public const int DrainerMinReach = 2; // a destination draining ≥ N distinct weak addrs is a drainer
    public const int ActiveInbound = 2;   // re-funded ≥ this many times ⇒ active wallet
    public const int ActiveHoldDays = 7;  // or held at least this long

    public enum Bucket { Live, CustodyLatency, Drain, CustodyActive, OneShot }

    /// <param name="maxReach">Max, over this finding's sweepers, of the distinct weak
    /// addresses that sweeper drained (1 = single-use destination).</param>
    /// <param name="minLatencyDays">Min funding→spend latency for this address, or null if
    /// no per-sweep latency is available (then a same-day activity window is the proxy).</param>
    public static Bucket Of(Finding f, int maxReach, int? minLatencyDays)
    {
        if (f.OutboundTxs == 0) return Bucket.Live;
        bool raced = minLatencyDays is int d ? d <= RacedMaxDays : HeldDays(f) == 0;
        if (!raced) return Bucket.CustodyLatency;
        if (maxReach >= DrainerMinReach) return Bucket.Drain;
        if (f.InboundTxs >= ActiveInbound || HeldDays(f) >= ActiveHoldDays) return Bucket.CustodyActive;
        return Bucket.OneShot;
    }

    public static bool IsCustody(Bucket b) => b is Bucket.CustodyLatency or Bucket.CustodyActive;

    public static int HeldDays(Finding f) =>
        DateTime.TryParse(f.FirstSeen, out var a) && DateTime.TryParse(f.LastSeen, out var b)
            ? (int)Math.Round((b - a).TotalDays) : 0;

    public static string Label(Bucket b) => b switch
    {
        Bucket.Live           => "live/unspent",
        Bucket.CustodyLatency => "custody — moved with latency (not raced)",
        Bucket.Drain          => "drain — instant + serial collector (bot or sweep-tooling)",
        Bucket.CustodyActive  => "custody — instant but active (re-funded/held)",
        Bucket.OneShot        => "one-shot — instant, single-use (self-test OR quiet drain)",
        _ => "?"
    };
}
