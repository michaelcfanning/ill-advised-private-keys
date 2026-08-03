using System.Collections.Concurrent;
using System.Diagnostics;
using NBitcoin;

namespace WeakKeyScanner;

public readonly record struct ScanStats(long Candidates, long Addresses, int Hits, TimeSpan Elapsed);

/// <summary>
/// Runs candidates through the pipeline. Detection (derive + offline membership)
/// is CPU-bound and runs in parallel across cores; enrichment of the few hits is
/// I/O-bound and runs sequentially afterward. Never signs.
/// </summary>
public sealed class Scanner
{
    private readonly IFundedOracle _oracle;
    private readonly EsploraClient? _enrich;
    private readonly FindingsWriter _findings;
    private readonly Network _network;
    private readonly int _indices;
    private readonly int _threads;
    private readonly long _progressEvery;

    public Scanner(IFundedOracle oracle, EsploraClient? enrich, FindingsWriter findings,
                   Network network, int indices = 1, int threads = 0, long progressEvery = 50_000)
    {
        _oracle = oracle;
        _enrich = enrich;
        _findings = findings;
        _network = network;
        _indices = indices;
        _threads = threads > 0 ? threads : Environment.ProcessorCount;
        _progressEvery = progressEvery;
    }

    public async Task<ScanStats> RunAsync(IEnumerable<WeakKey> keys, string patternType, CancellationToken ct = default)
    {
        long candidates = 0, addresses = 0;
        var hitQueue = new ConcurrentQueue<(WeakKey Key, DerivedAddress Addr)>();
        var sw = Stopwatch.StartNew();

        // Phase 1: parallel derive + offline detect (CPU-bound).
        // Materialize first (generation is cheap: entropy + checksum, no PBKDF2) so
        // Parallel.For range-partitions without an enumerator lock. The expensive
        // PBKDF2 + EC derivation happens inside the loop body, across all cores.
        var work = keys as WeakKey[] ?? keys.ToArray();
        var options = new ParallelOptions { MaxDegreeOfParallelism = _threads, CancellationToken = ct };
        Parallel.For(0, work.Length, options, i =>
        {
            var wk = work[i];
            foreach (var d in Bip39Deriver.Derive(wk.Mnemonic, _network, _indices))
            {
                Interlocked.Increment(ref addresses);
                var info = _oracle.LookupAsync(d.Address, ct).GetAwaiter().GetResult();
                if (info is not null) hitQueue.Enqueue((wk, d));
            }

            long c = Interlocked.Increment(ref candidates);
            if (_progressEvery > 0 && c % _progressEvery == 0)
                ReportProgress(c, Interlocked.Read(ref addresses), hitQueue.Count, sw);
        });

        // Phase 2: enrich hits (I/O-bound), verify, write.
        int hits = 0;
        foreach (var (wk, d) in hitQueue)
        {
            ct.ThrowIfCancellationRequested();
            AddressActivity a;
            if (_enrich is not null)
            {
                try { a = await _enrich.AnalyzeAsync(d.Address, ct); }
                catch { a = Marker(); }
                if (!a.EverFunded) continue; // hashed-set collision: not actually funded
            }
            else
            {
                a = Marker();
            }

            hits++;
            _findings.Write(Finding.From(patternType, wk.Mnemonic, d, a));
            Console.WriteLine($"  >>> HIT  {d.Kind}  {d.Address}");
            Console.WriteLine($"      key : {wk.Mnemonic}");
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
        Console.WriteLine($"... {candidates:N0} candidates | {addresses:N0} addresses | {hits} raw hits | {rate:N0} cand/s");
    }
}
