using System.Collections.Concurrent;
using System.Diagnostics;
using NBitcoin;

namespace WeakKeyScanner;

/// <summary>
/// Streams passwords through the brainwallet derivation in parallel, checks each
/// derived address against the funded oracle, and enriches the hits. Streams the
/// wordlist (never materializes) so multi-gigabyte lists like CrackStation fit.
/// </summary>
public sealed class BrainScanner
{
    private readonly IFundedOracle _oracle;
    private readonly EsploraClient? _enrich;
    private readonly FindingsWriter _findings;
    private readonly Network _network;
    private readonly int _threads;
    private readonly long _progressEvery;

    public BrainScanner(IFundedOracle oracle, EsploraClient? enrich, FindingsWriter findings,
                        Network network, int threads = 0, long progressEvery = 1_000_000)
    {
        _oracle = oracle;
        _enrich = enrich;
        _findings = findings;
        _network = network;
        _threads = threads > 0 ? threads : Environment.ProcessorCount;
        _progressEvery = progressEvery;
    }

    public async Task<ScanStats> RunAsync(IEnumerable<string> passwords, string patternType, CancellationToken ct = default)
    {
        long candidates = 0, addresses = 0;
        var hitQueue = new ConcurrentQueue<(string Password, DerivedAddress Addr)>();
        var sw = Stopwatch.StartNew();

        var options = new ParallelOptions { MaxDegreeOfParallelism = _threads, CancellationToken = ct };
        Parallel.ForEach(passwords, options, pw =>
        {
            foreach (var d in BrainwalletDeriver.Derive(pw, _network))
            {
                Interlocked.Increment(ref addresses);
                var info = _oracle.LookupAsync(d.Address, ct).GetAwaiter().GetResult();
                if (info is not null) hitQueue.Enqueue((pw, d));
            }

            long c = Interlocked.Increment(ref candidates);
            if (_progressEvery > 0 && c % _progressEvery == 0)
                ReportProgress(c, Interlocked.Read(ref addresses), hitQueue.Count, sw);
        });

        int hits = 0;
        foreach (var (pw, d) in hitQueue)
        {
            ct.ThrowIfCancellationRequested();
            AddressActivity a;
            if (_enrich is not null)
            {
                try { a = await _enrich.AnalyzeAsync(d.Address, ct); }
                catch { a = Marker(); }
                if (!a.EverFunded) continue;
            }
            else
            {
                a = Marker();
            }

            hits++;
            _findings.Write(Finding.From(patternType, pw, d, a));
            Console.WriteLine($"  >>> HIT  {d.Kind}  {d.Address}");
            Console.WriteLine($"      password : {pw}");
            Console.WriteLine($"      {Format.Activity(a)}");
            Console.WriteLine($"      {Explorers.Address(d.Address)}");
        }

        sw.Stop();
        return new ScanStats(candidates, addresses, hits, sw.Elapsed);
    }

    private static AddressActivity Marker() => new(
        true, 0, 0, 0, null, null, 0, 0, Array.Empty<string>(), Array.Empty<string>());

    private static void ReportProgress(long candidates, long addresses, int hits, Stopwatch sw)
    {
        double rate = candidates / Math.Max(0.001, sw.Elapsed.TotalSeconds);
        Console.WriteLine($"... {candidates:N0} passwords | {addresses:N0} addresses | {hits} raw hits | {rate:N0} pw/s");
    }
}
