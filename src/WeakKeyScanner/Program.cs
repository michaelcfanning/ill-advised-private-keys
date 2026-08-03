using NBitcoin;
using WeakKeyScanner;

// ill-advised-private-keys — weak key scanner
//
//   (no args) | control      Positive-control test: the zero-entropy vector.
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
    if (args.Length < 2) { Console.Error.WriteLine("usage: analyze <findings.jsonl>"); return 2; }
    return Analyze.Run(args[1]);
}

if (args[0] == "brainscan")
{
    return await RunBrainScan(args);
}

Console.Error.WriteLine($"unknown command: {args[0]}");
return 2;

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
    public static int Run(string path)
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
