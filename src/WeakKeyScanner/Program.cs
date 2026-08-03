using NBitcoin;
using WeakKeyScanner;

// ill-advised-private-keys — weak key scanner
//
//   (no args) | control      Positive-control test: the zero-entropy vector.
//   selftest --set FILE      Prove the offline oracle can emit a true positive.
//   analyze FILE [--events OUT]  Classify findings into compromise events (ECONOMICS.md).
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
    string? eventsOut = null;
    for (int i = 2; i < args.Length; i++)
    {
        switch (args[i])
        {
            case "--events": eventsOut = args[++i]; break;
            default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
        }
    }
    return Analyze.Run(args[1], eventsOut);
}

if (args[0] == "brainscan")
{
    return await RunBrainScan(args);
}

if (args[0] == "selftest")
{
    return RunSelfTest(args);
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
    public static int Run(string path, string? eventsOut = null)
    {
        if (!File.Exists(path)) { Console.Error.WriteLine($"not found: {path}"); return 2; }

        var findings = File.ReadLines(path)
            .Where(l => l.Trim().Length > 0)
            .Select(l => System.Text.Json.JsonSerializer.Deserialize<Finding>(l)!)
            .ToList();

        if (findings.Count == 0) { Console.WriteLine("no findings."); return 0; }

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

        if (eventsOut is not null)
        {
            Econ.WriteEvents(events, eventsOut);
            Console.WriteLine();
            Console.WriteLine($"compromise events written: {System.IO.Path.GetFullPath(eventsOut)} ({events.Count} records)");
        }

        return 0;
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
