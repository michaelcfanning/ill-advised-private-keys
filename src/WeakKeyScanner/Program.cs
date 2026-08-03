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

Console.Error.WriteLine($"unknown command: {args[0]}");
return 2;

static async Task<int> RunScan(string[] args)
{
    var o = Options.Parse(args);

    IFundedOracle oracle;
    EsploraClient? enrich = null;
    var apiClient = new EsploraClient();

    if (o.Oracle == "offline")
    {
        if (o.SetFile is null) { Console.Error.WriteLine("offline mode requires --set FILE"); return 2; }
        if (!File.Exists(o.SetFile)) { Console.Error.WriteLine($"set file not found: {o.SetFile}"); return 2; }
        oracle = OfflineSetOracle.FromFile(o.SetFile);
        if (o.Enrich) enrich = apiClient;
    }
    else
    {
        oracle = new EsploraApiOracle(apiClient);
    }

    var patternType = $"repeated-word/{o.WordCount}" + (o.FirstOnly ? "/first-completion" : "");
    using var findings = new FindingsWriter(o.OutFile);

    Console.WriteLine("ill-advised-private-keys — weak key scanner");
    Console.WriteLine($"pattern     : {patternType}");
    Console.WriteLine($"words       : [{o.FromWord}, {o.ToWord})");
    Console.WriteLine($"detection   : {oracle.Describe()}");
    Console.WriteLine($"enrichment  : {(enrich is null ? "none" : "esplora API (hits only)")}");
    Console.WriteLine($"out         : {findings.Path}");
    Console.WriteLine(new string('=', 72));

    var keys = RepeatedWordGenerator.All(o.WordCount, o.FromWord, o.ToWord, o.FirstOnly);
    var scanner = new Scanner(oracle, enrich, findings, Network.Main, o.Indices);

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
    int WordCount, int FromWord, int ToWord, bool FirstOnly,
    string Oracle, string? SetFile, int Indices, bool Enrich, string OutFile)
{
    public static Options Parse(string[] args)
    {
        int wordCount = 12, indices = 1, words = 2048, from = 0, to = -1;
        bool firstOnly = false, enrich = false;
        string oracle = "offline", outFile = Path.Combine(AppContext.BaseDirectory, "out", "scan.findings.jsonl");
        string? set = null;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--wordcount": wordCount = int.Parse(args[++i]); break;
                case "--words": words = int.Parse(args[++i]); break;
                case "--from": from = int.Parse(args[++i]); break;
                case "--to": to = int.Parse(args[++i]); break;
                case "--completions": firstOnly = args[++i] == "first"; break;
                case "--oracle": oracle = args[++i]; break;
                case "--set": set = args[++i]; break;
                case "--indices": indices = int.Parse(args[++i]); break;
                case "--enrich": enrich = true; break;
                case "--out": outFile = args[++i]; break;
                default: throw new ArgumentException($"unknown option: {args[i]}");
            }
        }

        int fromWord = from;
        int toWord = to >= 0 ? to : Math.Min(2048, from + words);
        return new Options(wordCount, fromWord, toWord, firstOnly, oracle, set, indices, enrich, outFile);
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
            FundingInfo f;
            try { f = await oracle.CheckAsync(d.Address); }
            catch (Exception ex) { Console.WriteLine($"[ERROR ] {d.Kind}: {ex.Message}\n"); continue; }

            var tag = f.EverFunded ? (f.BalanceSats > 0 ? "[LIVE  ]" : "[FUNDED]") : "[clean ]";
            Console.WriteLine($"{tag} {d.Kind}");
            Console.WriteLine($"    weak key : {AbandonVector}");
            Console.WriteLine($"    address  : {d.Address}");
            Console.WriteLine($"    path     : {d.Path}");
            Console.WriteLine($"    activity : {Format.Activity(f)}");
            Console.WriteLine($"    explorer : {Explorers.Address(d.Address)}");
            Console.WriteLine();

            if (f.EverFunded)
            {
                funded++;
                findings.Write(new Finding(
                    PatternType, AbandonVector, d.Kind, d.Path, "bitcoin", d.Address,
                    f.EverFunded, f.TxCount, f.TotalReceivedSats, f.BalanceSats,
                    Explorers.Address(d.Address)));
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
