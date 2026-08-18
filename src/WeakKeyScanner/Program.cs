using NBitcoin;
using WeakKeyScanner;

// ill-advised-private-keys — weak key scanner
//
//   (no args) | control      Positive-control test: the zero-entropy vector.
//   selftest --set FILE      Prove the offline oracle can emit a true positive.
//   analyze FILE [--events OUT] [--latencies LAT]  Classify findings; adds per-family
//                            prevalence and the sweep-latency distribution (ECONOMICS.md).
//   bcscan --weakset F --dumps D  Ever-funded oracle: intersect weak addrs w/ Blockchair outputs.
//   emit [options]           Serialize enumerated weak candidate addresses to a TSV (weakset producer).
//                            --raw [--width W] [--f1] [--f2]  raw target-K fills (F1/F2); --append to accumulate.
//   nodewalk --weakset F     Ever-funded + sweep index straight from local bitcoind (resumable).
//   scan [options]           Enumerate repeated-word candidates and check funding.
//
// scan options:
//   --wordcount 12|24        mnemonic length            (default 12)
//   --words N                first N repeated words      (default 2048)
//   --from A --to B          word-index range [A, B)     (overrides --words)
//   --completions all|first  all valid completions, or only the free-bits=0 one
//   --oracle offline|api     detection source            (default offline)
//   --set FILE               funded-address file (required for offline)
//   --indices N              address indices per path    (default 1)
//   --enrich                 fill hit details via API (offline mode)
//   --out FILE               findings JSONL              (default out/scan.findings.jsonl)

if (args.Length == 0 || args[0] == "control")
{
    return await Control.RunAsync();
}

if (args[0] == "scan")
{
    return await RunScan(args);
}

if (args[0] == "analyze")
{
    if (args.Length < 2) { Console.Error.WriteLine("usage: analyze <findings.jsonl> [--events OUT.jsonl]"); return 2; }
    string? eventsOut = null, latPath = null, pricesPath = null;
    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--events": eventsOut = args[++i]; break;
            case "--latencies": latPath = args[++i]; break;
            case "--prices": pricesPath = args[++i]; break;
            default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
        }
    }
    return Analyze.Run(args[1], eventsOut, latPath, pricesPath);
}

if (args[0] == "brainscan")
{
    return await RunBrainScan(args);
}

if (args[0] == "selftest")
{
    return RunSelfTest(args);
}

if (args[0] == "bcscan")
{
    return BlockchairScan.Run(args);
}

if (args[0] == "emit")
{
    return EmitSet.Run(args);
}

if (args[0] == "nodewalk")
{
    return NodeWalk.Run(args);
}

Console.Error.WriteLine($"unknown command: {args[0]}");
return 2;

// selftest --set FILE
//
// A null result from the offline oracle is ambiguous: the space may be swept clean,
// OR the matcher may be silently blind (e.g. our derived address strings don't match
// the set's string form, so every real funded address misses). This proves the
// matcher can produce a true positive, so an all-null scan means "clean", not "blind":
//   1. derivation  — privkey=1 through the real NBitcoin path == the documented address
//   2. format      — for each address type in the set, NBitcoin's canonical form is
//                    byte-identical to the set's form and hits the matcher
//   3. matcher     — a known-present token hits; a garbage string misses
static int RunSelfTest(string[] args)
{
    string? set = null;
    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--set": set = args[++i]; break;
            default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
        }
    }
    if (set is null || !File.Exists(set)) { Console.Error.WriteLine("selftest requires --set FILE"); return 2; }

    int failures = 0;
    void Check(bool ok, string label)
    {
        Console.WriteLine($"  [{(ok ? "PASS" : "FAIL")}] {label}");
        if (!ok) failures++;
    }

    Console.WriteLine("ill-advised-private-keys - offline oracle self-test");
    Console.WriteLine(new string('=', 72));

    // 1. Derivation correctness, independent of any set: privkey = 1 has a single,
    //    universally-documented pair of P2PKH addresses. If our encoder is right,
    //    both fall out of the same Key -> PubKey -> GetAddress path the scanner uses.
    Console.WriteLine("derivation (privkey=1, canonical ground truth):");
    byte[] one = new byte[32]; one[31] = 1;
    string dc = new Key(one, -1, fCompressedIn: true).PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();
    string du = new Key(one, -1, fCompressedIn: false).PubKey.GetAddress(ScriptPubKeyType.Legacy, Network.Main).ToString();
    Check(dc == "1BgGZ9tcN4rm9KBzDn7KprQz87SZ26SAMH", $"compressed   -> {dc}");
    Check(du == "1EHNa6Q4Jz2uvNExL497mE43ikXhwF6kZm", $"uncompressed -> {du}");

    Console.WriteLine($"loading set: {set}");
    var (oracle, first, samples) = OfflineSetOracle.LoadWithSamples(set);
    Console.WriteLine($"{oracle.Describe()}");

    // 2. Format equivalence: for every address family present in the set, prove that
    //    NBitcoin's canonical string form (exactly what our derivation emits) matches
    //    the set's stored form byte-for-byte AND registers as a hit. A bech32-casing
    //    or encoding mismatch here is precisely the silent-miss failure we fear.
    Console.WriteLine("format equivalence (NBitcoin canonical form == set form, per type):");
    foreach (var (type, sample) in samples.OrderBy(kv => kv.Key))
    {
        string canon;
        try { canon = BitcoinAddress.Create(sample, Network.Main).ToString(); }
        catch { Check(false, $"{type}: NBitcoin cannot parse set form ({sample})"); continue; }
        Check(canon == sample && oracle.Contains(canon), $"{type}: {sample}");
    }

    // 3. Matcher sanity: a token we know is in the set must hit; a token we know is
    //    not must miss. Guards against a matcher that is blind or matches everything.
    Console.WriteLine("matcher sanity:");
    if (first is null)
    {
        Check(false, "no recognizable address found in set (cannot test membership)");
    }
    else
    {
        Check(oracle.Contains(first), $"known-present address hits ({first})");
        Check(!oracle.Contains("zzz-not-a-real-address-" + first), "known-absent address misses");
    }

    Console.WriteLine(new string('=', 72));
    Console.WriteLine(failures == 0
        ? "SELF-TEST PASSED - an all-null offline scan means the space is clean, not blind."
        : $"SELF-TEST FAILED ({failures}) - offline null results are NOT trustworthy until fixed.");
    return failures == 0 ? 0 : 1;
}

// brainscan --wordlist FILE [--oracle offline|api] [--set FILE] [--threads N]
//           [--no-enrich] [--out FILE]
static async Task<int> RunBrainScan(string[] args)
{
    string? wordlist = null, set = null;
    string oracleKind = "offline";
    string outFile = Path.Combine(AppContext.BaseDirectory, "out", "brain.findings.jsonl");
    int threads = 0;
    bool noEnrich = false;

    for (int i = 1; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--wordlist": wordlist = args[++i]; break;
            case "--oracle": oracleKind = args[++i]; break;
            case "--set": set = args[++i]; break;
            case "--threads": threads = int.Parse(args[++i]); break;
            case "--no-enrich": noEnrich = true; break;
            case "--out": outFile = args[++i]; break;
            default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
        }
    }
    if (wordlist is null || !File.Exists(wordlist)) { Console.Error.WriteLine("brainscan requires --wordlist FILE"); return 2; }

    var apiClient = new EsploraClient();
    IFundedOracle oracle;
    if (oracleKind == "offline")
    {
        if (set is null || !File.Exists(set)) { Console.Error.WriteLine("offline mode requires --set FILE"); return 2; }
        oracle = OfflineSetOracle.FromFile(set);
    }
    else
    {
        oracle = new EsploraApiOracle(apiClient);
    }
    EsploraClient? enrich = noEnrich ? null : apiClient;

    using var findings = new FindingsWriter(outFile);
    Console.WriteLine("ill-advised-private-keys — brainwallet scan");
    Console.WriteLine($"wordlist    : {wordlist}");
    Console.WriteLine($"detection   : {oracle.Describe()}");
    Console.WriteLine($"threads     : {(threads > 0 ? threads : Environment.ProcessorCount)}");
    Console.WriteLine($"out         : {findings.Path}");
    Console.WriteLine(new string('=', 72));

    // Stream the wordlist so multi-GB files never materialize.
    IEnumerable<string> Passwords() => File.ReadLines(wordlist).Where(l => l.Length > 0);

    var scanner = new BrainScanner(oracle, enrich, findings, Network.Main, threads);
    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    ScanStats s;
    try { s = await scanner.RunAsync(Passwords(), "brainwallet/sha256", cts.Token); }
    catch (OperationCanceledException) { Console.WriteLine("\n[cancelled]"); return 130; }
    finally { apiClient.Dispose(); }

    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"done: {s.Candidates:N0} passwords | {s.Addresses:N0} addresses | {s.Hits} hits | {s.Elapsed.TotalSeconds:N1}s");
    Console.WriteLine($"findings: {findings.Path}");
    return 0;
}

static async Task<int> RunScan(string[] args)
{
    var o = Options.Parse(args);

    IFundedOracle oracle;
    var apiClient = new EsploraClient();

    if (o.Oracle == "offline")
    {
        if (o.SetFile is null) { Console.Error.WriteLine("offline mode requires --set FILE"); return 2; }
        if (!File.Exists(o.SetFile)) { Console.Error.WriteLine($"set file not found: {o.SetFile}"); return 2; }
        oracle = OfflineSetOracle.FromFile(o.SetFile);
    }
    else
    {
        oracle = new EsploraApiOracle(apiClient);
    }

    // The few hits are always deep-analyzed (funders, sweepers, dates) unless disabled.
    EsploraClient? enrich = o.NoEnrich ? null : apiClient;

    var spec = PatternSpec.Parse(o.Pattern);
    var patternType = $"{spec.Name}/{o.WordCount}" + (o.FirstOnly ? "/first-completion" : "");
    using var findings = new FindingsWriter(o.OutFile, o.Append);

    Console.WriteLine("ill-advised-private-keys — weak key scanner");
    Console.WriteLine($"pattern     : {patternType}");
    Console.WriteLine($"words       : [{o.FromWord}, {o.ToWord})");
    Console.WriteLine($"detection   : {oracle.Describe()}");
    Console.WriteLine($"threads     : {(o.Threads > 0 ? o.Threads : Environment.ProcessorCount)}");
    Console.WriteLine($"enrichment  : {(enrich is null ? "none" : "esplora API (hits only: funders/sweepers/dates)")}");
    Console.WriteLine($"out         : {findings.Path}{(o.Append ? " (append)" : "")}");
    Console.WriteLine(new string('=', 72));

    var keys = PatternGenerator.All(spec, o.WordCount, o.FromWord, o.ToWord, o.FirstOnly);
    var scanner = new Scanner(oracle, enrich, findings, Network.Main, o.Indices, o.Threads);

    using var cts = new CancellationTokenSource();
    Console.CancelKeyPress += (_, e) => { e.Cancel = true; cts.Cancel(); };

    ScanStats s;
    try { s = await scanner.RunAsync(keys, patternType, cts.Token); }
    catch (OperationCanceledException) { Console.WriteLine("\n[cancelled]"); return 130; }
    finally { apiClient.Dispose(); }

    Console.WriteLine(new string('=', 72));
    Console.WriteLine($"done: {s.Candidates:N0} candidates | {s.Addresses:N0} addresses | {s.Hits} hits | {s.Elapsed.TotalSeconds:N1}s");
    Console.WriteLine($"findings: {findings.Path}");
    return 0;
}

file sealed record Options(
    string Pattern, int WordCount, int FromWord, int ToWord, bool FirstOnly,
    string Oracle, string? SetFile, int Indices, int Threads, bool NoEnrich, bool Append, string OutFile)
{
    public static Options Parse(string[] args)
    {
        int wordCount = 12, indices = 1, words = 2048, from = 0, to = -1, threads = 0;
        bool firstOnly = false, noEnrich = false, append = false;
        string oracle = "offline", pattern = "repeat";
        string outFile = Path.Combine(AppContext.BaseDirectory, "out", "scan.findings.jsonl");
        string? set = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pattern": pattern = args[++i]; break;
                case "--wordcount": wordCount = int.Parse(args[++i]); break;
                case "--words": words = int.Parse(args[++i]); break;
                case "--from": from = int.Parse(args[++i]); break;
                case "--to": to = int.Parse(args[++i]); break;
                case "--completions": firstOnly = args[++i] == "first"; break;
                case "--oracle": oracle = args[++i]; break;
                case "--set": set = args[++i]; break;
                case "--indices": indices = int.Parse(args[++i]); break;
                case "--threads": threads = int.Parse(args[++i]); break;
                case "--no-enrich": noEnrich = true; break;
                case "--append": append = true; break;
                case "--out": outFile = args[++i]; break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }

        int fromWord = from;
        int toWord = to >= 0 ? to : Math.Min(2048, from + words);
        return new Options(pattern, wordCount, fromWord, toWord, firstOnly, oracle, set, indices, threads, noEnrich, append, outFile);
    }
}

file static class Analyze
{
    public static int Run(string path, string? eventsOut = null, string? latPath = null, string? pricesPath = null)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"not found: {path}"); return 2; }

        var findings = File.ReadLines(path)
            .Where(l => l.Trim().Length > 0)
            .Select(l => System.Text.Json.JsonSerializer.Deserialize<Finding>(l)!)
            .ToList();

        if (findings.Count == 0) { Console.WriteLine("no findings."); return 0; }

        Prices? px = pricesPath is not null && File.Exists(pricesPath) ? Prices.Load(pricesPath) : null;

        // sweeper address -> distinct compromised addresses it drained
        var sweeperReach = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        var funderReach = new Dictionary<string, HashSet<string>>(StringComparer.Ordinal);
        foreach (var f in findings)
        {
            foreach (var s in f.Sweepers)
                (sweeperReach.TryGetValue(s, out var set) ? set : sweeperReach[s] = new(StringComparer.Ordinal)).Add(f.Address);
            foreach (var fu in f.Funders)
                (funderReach.TryGetValue(fu, out var set) ? set : funderReach[fu] = new(StringComparer.Ordinal)).Add(f.Address);
        }

        var weakAddrs = findings.Select(f => f.Address).Distinct().Count();
        long received = findings.Sum(f => f.TotalReceivedSats);
        var dates = findings.Where(f => f.FirstSeen is not null).Select(f => f.FirstSeen!).ToList();
        var lasts = findings.Where(f => f.LastSeen is not null).Select(f => f.LastSeen!).ToList();

        Console.WriteLine("ill-advised-private-keys — findings analysis");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"findings              : {findings.Count}");
        Console.WriteLine($"distinct weak addrs   : {weakAddrs}");
        Console.WriteLine($"total received        : {Format.Btc(received)}");
        if (dates.Count > 0)
            Console.WriteLine($"activity window       : {dates.Min()}  ..  {lasts.Max()}");
        Console.WriteLine($"distinct funders      : {funderReach.Count}");
        Console.WriteLine($"distinct sweepers     : {sweeperReach.Count}");
        Console.WriteLine();

        Console.WriteLine("top sweepers (drainer set — by # of weak addresses drained):");
        foreach (var (addr, set) in sweeperReach.OrderByDescending(kv => kv.Value.Count).Take(15))
            Console.WriteLine($"  {set.Count,4}  {addr}");

        // ---- Economic classification (ECONOMICS.md) ----
        var (keys, addrs) = Econ.SeedDenylist();
        var events = findings.Select(f => Econ.ToEvent(f, keys, addrs)).ToList();

        // ---- Per-family prevalence (family recovered from each mnemonic) ----
        Console.WriteLine();
        Console.WriteLine("prevalence by pattern family:");
        var paired = findings.Zip(events, (f, e) => (f, e));
        foreach (var g in paired.GroupBy(x => Family.Classify(x.f.WeakKey)).OrderByDescending(g => g.Count()))
        {
            int funded = g.Select(x => x.f.Address).Distinct().Count();
            decimal recv = g.Sum(x => x.f.TotalReceivedSats) / 100_000_000m;
            int victims = g.Count(x => x.e.Classification == "victim");
            Console.WriteLine($"  {g.Key,-12}  {funded,5} funded  {recv,16:0.########} BTC  {victims,5} victims");
        }

        // ---- Actor view: researchers / general users / larkers / bad guys ----
        // Disposition (raced-vs-custody) plus who deposited and whether a sweep is a theft.
        // USD is valued at the deposit date (≈ sweep date for same-day drains), so loss reads
        // in the money of the time, not today's price.
        var minLat = latPath is not null && File.Exists(latPath) ? ReadMinLatency(latPath) : null;
        int MaxReach(Finding f) => f.Sweepers.Length == 0 ? 0
            : f.Sweepers.Max(s => sweeperReach.TryGetValue(s, out var set) ? set.Count : 0);
        int? Lat(Finding f) => minLat is not null && minLat.TryGetValue(f.Address, out var d) ? d : (int?)null;
        var seedSet = Classify.SeedingClusters(findings);
        var (denyKeys, _) = Econ.SeedDenylist();
        decimal UsdAt(Finding f) => px is null ? 0m : (px.Usd(f.FirstSeen) ?? 0m) * (f.TotalReceivedSats / 100_000_000m);

        var byActor = findings
            .Select(f => (f, a: Classify.ActorOf(f, MaxReach(f), Lat(f),
                seedSet.Contains(f.Address), Classify.IsRecognizable(f.WeakKey, denyKeys))))
            .GroupBy(x => x.a)
            .ToDictionary(g => g.Key, g => g.ToList());

        Console.WriteLine();
        Console.WriteLine("actor view (researchers / general users / larkers / bad guys):");
        if (minLat is null) Console.WriteLine("  (no --latencies: same-day activity window used as the race proxy)");
        Console.WriteLine($"  {"actor",-42}{"addrs",7}{"keys",7}{"BTC",13}" + (px is null ? "" : $"{"USD@time",14}"));
        foreach (var a in new[] { Classify.Actor.Victim, Classify.Actor.Lark, Classify.Actor.Unattributed,
                                  Classify.Actor.Custody, Classify.Actor.Honeypot, Classify.Actor.Live })
        {
            if (!byActor.TryGetValue(a, out var list)) continue;
            int nkeys = list.Select(x => x.f.WeakKey).Distinct().Count();
            decimal btc = list.Sum(x => x.f.TotalReceivedSats) / 100_000_000m;
            string usd = px is null ? "" : $"{list.Sum(x => UsdAt(x.f)),14:C0}";
            Console.WriteLine($"  {Classify.ActorLabel(a),-42}{list.Count,7:N0}{nkeys,7:N0}{btc,13:0.####}{usd}");
        }
        Console.WriteLine("  (bad guys = the sweepers above; a sweep of honeypot or lark funds is not a victim loss)");

        // ---- Loss concentration across passwords ----
        var perKey = findings.GroupBy(f => f.WeakKey)
            .Select(g => (Key: g.Key, Btc: g.Sum(f => f.TotalReceivedSats) / 100_000_000m,
                          Usd: g.Sum(UsdAt), Famous: Classify.IsRecognizable(g.Key, denyKeys)))
            .OrderByDescending(x => x.Btc).ToList();
        decimal totBtc = perKey.Sum(x => x.Btc);
        if (perKey.Count > 0 && totBtc > 0)
        {
            decimal maxBtc = perKey[0].Btc;
            Console.WriteLine();
            Console.WriteLine("loss concentration (received value per password, top 15):");
            int rank = 0;
            foreach (var x in perKey.Take(15))
            {
                rank++;
                int bar = (int)Math.Round(28 * (double)(x.Btc / maxBtc));
                string usd = px is null ? "" : $" {x.Usd,11:C0}@time";
                Console.WriteLine($"  {rank,2} {new string('#', bar),-28} {x.Btc,9:0.####} BTC{usd}  {(x.Famous ? "[famous] " : "")}{Trunc(x.Key, 30)}");
            }
            decimal top5 = perKey.Take(5).Sum(x => x.Btc), top15 = perKey.Take(15).Sum(x => x.Btc);
            decimal famousBtc = perKey.Where(x => x.Famous).Sum(x => x.Btc);
            Console.WriteLine($"  top 5 = {top5 / totBtc:P1} of value | top 15 = {top15 / totBtc:P1} | Gini = {Gini(perKey.Select(x => x.Btc)):0.###}");
            Console.WriteLine($"  structurally-famous (keyboard walks/runs/repeats) = {famousBtc / totBtc:P1} of value, " +
                              $"{perKey.Count(x => x.Famous):N0}/{perKey.Count:N0} keys — a LOWER bound on larks.");
            Console.WriteLine("  every password here is dictionary-guessable and value concentrates on trivially-weak strings,");
            Console.WriteLine("  so 'good-faith victim' is an UPPER bound: the lark/victim intent is not resolvable per deposit.");
        }

        Console.WriteLine();
        Console.WriteLine("compromise events by classification:");
        foreach (var g in events.GroupBy(e => e.Classification).OrderByDescending(g => g.Count()))
        {
            decimal btc = g.Sum(e => e.DepositValueBtc);
            Console.WriteLine($"  {g.Count(),5}  {g.Key,-22}  deposits {btc:0.########} BTC");
        }

        // Loss reported three ways, per ECONOMICS.md (never one line-drawing).
        decimal Loss(params string[] cls) =>
            events.Where(e => cls.Contains(e.Classification)).Sum(e => e.DepositValueBtc);
        Console.WriteLine();
        Console.WriteLine("loss basis (deposit BTC; USD-at-time is node-gated, pending index):");
        Console.WriteLine($"  victims only          : {Loss("victim"):0.########} BTC");
        Console.WriteLine($"  victims + ambiguous   : {Loss("victim", "ambiguous"):0.########} BTC");
        Console.WriteLine($"  all events            : {events.Sum(e => e.DepositValueBtc):0.########} BTC");

        if (px is not null)
        {
            decimal Usd(params string[] cls) => events
                .Where(e => cls.Contains(e.Classification))
                .Sum(e => (px.Usd(e.FirstSeen) ?? 0m) * e.DepositValueBtc);
            Console.WriteLine();
            Console.WriteLine($"USD loss basis (deposit valued at first-seen-date close; {px.Count:N0} daily prices):");
            Console.WriteLine($"  victims only          : ${Usd("victim"):N0}");
            Console.WriteLine($"  victims + ambiguous   : ${Usd("victim", "ambiguous"):N0}");
            Console.WriteLine($"  all events            : ${events.Sum(e => (px.Usd(e.FirstSeen) ?? 0m) * e.DepositValueBtc):N0}");
        }

        // Arrival series — the freshness signal (needs ever-funded first_seen to be
        // complete; here it is over the current finding set only).
        var arrivals = events
            .Where(e => e.Classification is "victim" or "ambiguous" && e.FirstSeen is { Length: >= 4 })
            .GroupBy(e => e.FirstSeen![..4])
            .OrderBy(g => g.Key)
            .ToList();
        if (arrivals.Count > 0)
        {
            Console.WriteLine();
            Console.WriteLine("victim/ambiguous first-funding by year (arrival rate):");
            foreach (var g in arrivals)
                Console.WriteLine($"  {g.Key}  {new string('#', Math.Min(40, g.Count()))} {g.Count()}");
        }

        if (latPath is not null && File.Exists(latPath))
            ReportLatencies(latPath);

        if (eventsOut is not null)
        {
            var toWrite = px is null ? events : events.Select(e =>
                px.Usd(e.FirstSeen) is decimal u
                    ? e with { DepositValueUsdAtDeposit = Math.Round(u * e.DepositValueBtc, 2) }
                    : e).ToList();
            Econ.WriteEvents(toWrite, eventsOut);
            Console.WriteLine();
            Console.WriteLine($"compromise events written: {System.IO.Path.GetFullPath(eventsOut)} ({toWrite.Count} records)");
        }

        return 0;
    }

    // Per-address minimum funding→spend latency (days), from the node-walk latencies TSV.
    static Dictionary<string, int> ReadMinLatency(string path)
    {
        var d = new Dictionary<string, int>(StringComparer.Ordinal);
        using var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
        using var sr = new StreamReader(fs);
        bool header = true;
        string? line;
        while ((line = sr.ReadLine()) is not null)
        {
            if (header) { header = false; continue; }
            var c = line.Split('\t');
            if (c.Length < 4 || !int.TryParse(c[3], out int lat) || lat < 0) continue;
            if (!d.TryGetValue(c[0], out int cur) || lat < cur) d[c[0]] = lat;
        }
        return d;
    }

    static string Trunc(string s, int n) => s.Length <= n ? s : s.Substring(0, n - 2) + "..";

    // Gini coefficient of a value distribution (0 = even, →1 = all in one). Standard discrete
    // form over ascending-sorted positive values.
    static decimal Gini(IEnumerable<decimal> vals)
    {
        var xs = vals.Where(v => v > 0).OrderBy(v => v).ToList();
        int n = xs.Count;
        if (n == 0) return 0m;
        decimal total = 0m; for (int i = 0; i < n; i++) total += xs[i];
        if (total == 0m) return 0m;
        decimal wsum = 0m; for (int i = 0; i < n; i++) wsum += (i + 1) * xs[i];
        return (2m * wsum) / (n * total) - (decimal)(n + 1) / n;
    }

    static int Pct(List<int> s, int p)
    {
        if (s.Count == 0) return 0;
        int i = (int)Math.Ceiling(p / 100.0 * s.Count) - 1;
        return s[Math.Clamp(i, 0, s.Count - 1)];
    }

    static void ReportLatencies(string path)
    {
        var days = new List<int>();
        long swept = 0;
        var byYear = new SortedDictionary<string, List<int>>(StringComparer.Ordinal);
        bool header = true;
        // FileShare.ReadWrite so a still-running nodewalk holding the file open
        // doesn't block the read (and lets us peek at partial results mid-run).
        using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite))
        using (var sr = new StreamReader(fs))
        {
            string? line;
            while ((line = sr.ReadLine()) is not null)
            {
                if (header) { header = false; continue; }
                var c = line.Split('\t');
                if (c.Length < 5) continue;
                if (int.TryParse(c[3], out int d) && d >= 0)
                {
                    days.Add(d);
                    var yr = c[1].Length >= 4 ? c[1][..4] : "?";
                    (byYear.TryGetValue(yr, out var l) ? l : byYear[yr] = new()).Add(d);
                }
                if (long.TryParse(c[4], out long v)) swept += v;
            }
        }
        Console.WriteLine();
        Console.WriteLine("sweep-latency distribution (days, funding -> first spend):");
        if (days.Count == 0) { Console.WriteLine("  (no sweep events)"); return; }
        days.Sort();
        Console.WriteLine($"  n={days.Count:N0}  min={days[0]}  p25={Pct(days, 25)}  median={Pct(days, 50)}  " +
                          $"p75={Pct(days, 75)}  p90={Pct(days, 90)}  max={days[^1]}  mean={days.Average():0.0}");
        Console.WriteLine($"  swept value (sum of sweep events): {swept / 100_000_000m:0.########} BTC");
        Console.WriteLine("  median latency by funding year:");
        foreach (var (yr, l) in byYear) { l.Sort(); Console.WriteLine($"    {yr}  n={l.Count,6:N0}  median={Pct(l, 50)}d"); }
    }
}

file static class Control
{
    private const string PatternType = "repeated-word (control: zero-entropy vector)";
    private const string AbandonVector =
        "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

    public static async Task<int> RunAsync()
    {
        var outPath = Path.Combine(AppContext.BaseDirectory, "out", "control.findings.jsonl");

        Console.WriteLine("ill-advised-private-keys — weak key scanner");
        Console.WriteLine("Positive-control test: BIP-39 zero-entropy vector");
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"weak key: {AbandonVector}");
        Console.WriteLine();

        using var oracle = new EsploraClient();
        using var findings = new FindingsWriter(outPath);
        int scanned = 0, funded = 0;

        foreach (var d in Bip39Deriver.Derive(AbandonVector, Network.Main, count: 1))
        {
            scanned++;
            AddressActivity a;
            try { a = await oracle.AnalyzeAsync(d.Address); }
            catch (Exception ex) { Console.WriteLine($"[ERROR ] {d.Kind}: {ex.Message}\n"); continue; }

            var tag = a.EverFunded ? (a.BalanceSats > 0 ? "[LIVE  ]" : "[FUNDED]") : "[clean ]";
            Console.WriteLine($"{tag} {d.Kind}");
            Console.WriteLine($"    weak key : {AbandonVector}");
            Console.WriteLine($"    address  : {d.Address}");
            Console.WriteLine($"    path     : {d.Path}");
            Console.WriteLine($"    activity : {Format.Activity(a)}");
            Console.WriteLine($"    explorer : {Explorers.Address(d.Address)}");
            Console.WriteLine();

            if (a.EverFunded)
            {
                funded++;
                findings.Write(Finding.From(PatternType, AbandonVector, d, a));
            }
            await Task.Delay(400);
        }

        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"scanned {scanned} addresses | {funded} funded");
        Console.WriteLine($"findings written to: {findings.Path}");
        Console.WriteLine();
        Console.WriteLine(funded > 0
            ? "RESULT: positive signal — derivation + funded-check pipeline confirmed."
            : "RESULT: no signal — investigate the pipeline (derivation or oracle).");
        return funded > 0 ? 0 : 1;
    }
}
