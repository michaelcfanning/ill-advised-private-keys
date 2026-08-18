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

    public enum Bucket { Live, CustodyLatency, Drain, CustodyActive, OneShot, SelfTest }

    /// <param name="maxReach">Max, over this finding's sweepers, of the distinct weak
    /// addresses that sweeper drained (1 = single-use destination).</param>
    /// <param name="minLatencyDays">Min funding→spend latency for this address, or null if
    /// no per-sweep latency is available (then a same-day activity window is the proxy).</param>
    /// <param name="funderIsSweeper">True if an address that funded this weak address also
    /// swept it — the same entity in and out, so a self-test / self-move, not theft. Only the
    /// theft-suspect buckets (Drain, OneShot) are rescued to <see cref="Bucket.SelfTest"/>.</param>
    public static Bucket Of(Finding f, int maxReach, int? minLatencyDays, bool funderIsSweeper = false)
    {
        var b = Core(f, maxReach, minLatencyDays);
        if (funderIsSweeper && b is Bucket.Drain or Bucket.OneShot) return Bucket.SelfTest;
        return b;
    }

    private static Bucket Core(Finding f, int maxReach, int? minLatencyDays)
    {
        if (f.OutboundTxs == 0) return Bucket.Live;
        bool raced = minLatencyDays is int d ? d <= RacedMaxDays : HeldDays(f) == 0;
        if (!raced) return Bucket.CustodyLatency;
        if (maxReach >= DrainerMinReach) return Bucket.Drain;
        if (f.InboundTxs >= ActiveInbound || HeldDays(f) >= ActiveHoldDays) return Bucket.CustodyActive;
        return Bucket.OneShot;
    }

    public static bool IsCustody(Bucket b) => b is Bucket.CustodyLatency or Bucket.CustodyActive or Bucket.SelfTest;

    public static int HeldDays(Finding f) =>
        DateTime.TryParse(f.FirstSeen, out var a) && DateTime.TryParse(f.LastSeen, out var b)
            ? (int)Math.Round((b - a).TotalDays) : 0;

    // ---- Four-actor view (researchers / general users / larkers / bad guys) ----
    //
    // The disposition above asks "was it raced." The actor view asks "who put the money
    // there, and is a sweep a theft." Four actors show up on-chain:
    //   * researchers — seed many weak addresses to study sweepers (a honeypot campaign);
    //   * general users — a good-faith (if unwise) wallet: the only real victim if drained;
    //   * larkers — deposit to a *famous* weak string as a stunt, the way people send to
    //     Satoshi's address or privkey=1; a sweep here is self-inflicted, not victimhood;
    //   * bad guys — the sweepers who take it all (reported as the drainer population).

    public enum Actor { Honeypot, Lark, Victim, Custody, Unattributed, Live }

    /// <param name="seeded">Address is part of a detected seeding cluster (researcher honeypot).</param>
    /// <param name="famous">The weak key is a recognizable/famous string (larker, not victim).</param>
    public static Actor ActorOf(Finding f, int maxReach, int? minLatencyDays, bool seeded, bool famous)
    {
        if (seeded) return Actor.Honeypot;
        if (f.OutboundTxs == 0) return Actor.Live;
        if (famous) return Actor.Lark;
        bool raced = minLatencyDays is int d ? d <= RacedMaxDays : HeldDays(f) == 0;
        if (!raced) return Actor.Custody;
        if (maxReach >= DrainerMinReach) return Actor.Victim;
        if (f.InboundTxs >= ActiveInbound || HeldDays(f) >= ActiveHoldDays) return Actor.Custody;
        return Actor.Unattributed;
    }

    public static string ActorLabel(Actor a) => a switch
    {
        Actor.Honeypot     => "researcher / honeypot (seeding cluster)",
        Actor.Lark         => "larker / deliberate (famous weak string)",
        Actor.Victim       => "good-faith victim (drained)",
        Actor.Custody      => "general user (custody, self-moved)",
        Actor.Unattributed => "single-spend (unattributed)",
        Actor.Live         => "live / unspent",
        _ => "?"
    };

    /// <summary>
    /// Addresses in a seeding/honeypot campaign: a large set (≥ minCluster) each funded once
    /// with the *same* small amount in the same month — one actor seeding thousands, not
    /// organic use. Detects any such campaign (e.g. the Aug-2013 5,460-sat run) without a
    /// hardcoded amount. Funder counts would sharpen this but are not required.
    /// </summary>
    public static HashSet<string> SeedingClusters(IReadOnlyList<Finding> findings,
        int minCluster = 100, long maxAmountSats = 100_000)
    {
        var groups = new Dictionary<(long, string), List<string>>();
        foreach (var f in findings)
        {
            if (f.InboundTxs != 1 || f.TotalReceivedSats > maxAmountSats) continue;
            string month = f.FirstSeen is { Length: >= 7 } ? f.FirstSeen[..7] : "?";
            var key = (f.TotalReceivedSats, month);
            (groups.TryGetValue(key, out var l) ? l : groups[key] = new()).Add(f.Address);
        }
        var seed = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kv in groups)
            if (kv.Value.Count >= minCluster)
                foreach (var a in kv.Value) seed.Add(a);
        return seed;
    }

    /// <summary>
    /// A recognizable/famous weak string — a keyboard walk, a run, a repeat, something
    /// trivially short, or a known-published key. A large deposit to one of these is more
    /// plausibly a lark than a good-faith secret, so we treat it as deliberate rather than
    /// victim. A transparent heuristic, reported alongside the un-flagged figure, never as a
    /// precise line.
    /// </summary>
    public static bool IsRecognizable(string weakKey, ISet<string> denylist)
    {
        if (denylist.Contains(weakKey)) return true;
        string t = weakKey.Trim().ToLowerInvariant();
        if (t.Length is 0 or <= 4) return t.Length > 0;             // trivially short
        if (AllSame(t) || IsRun(t) || IsRepeatedUnit(t) || IsKeyboardWalk(t)) return true;
        return false;
    }

    private static bool AllSame(string t) { foreach (var c in t) if (c != t[0]) return false; return true; }

    private static bool IsRun(string t)
    {
        bool up = true, down = true;
        for (int i = 1; i < t.Length; i++) { if (t[i] != t[i - 1] + 1) up = false; if (t[i] != t[i - 1] - 1) down = false; }
        return up || down;
    }

    private static bool IsRepeatedUnit(string t)
    {
        for (int u = 1; u <= t.Length / 2; u++)
        {
            if (t.Length % u != 0) continue;
            bool ok = true;
            for (int i = u; i < t.Length && ok; i++) if (t[i] != t[i % u]) ok = false;
            if (ok) return true;
        }
        return false;
    }

    private static readonly string[] Rows =
        { "qwertyuiop", "asdfghjkl", "zxcvbnm", "1234567890", "qwertyuiopasdfghjklzxcvbnm" };

    private static bool IsKeyboardWalk(string t)
    {
        foreach (var r in Rows)
        {
            if (r.Contains(t)) return true;
            var rev = new string(r.Reverse().ToArray());
            if (rev.Contains(t)) return true;
        }
        return false;
    }

    public static string Label(Bucket b) => b switch
    {
        Bucket.Live           => "live/unspent",
        Bucket.CustodyLatency => "custody — moved with latency (not raced)",
        Bucket.Drain          => "drain — instant + serial collector (bot or sweep-tooling)",
        Bucket.CustodyActive  => "custody — instant but active (re-funded/held)",
        Bucket.OneShot        => "one-shot — instant, single-use (self-test OR quiet drain)",
        Bucket.SelfTest       => "self-test — funder reclaimed own deposit (funder == sweeper)",
        _ => "?"
    };
}
