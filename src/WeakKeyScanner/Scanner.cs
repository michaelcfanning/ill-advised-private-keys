using System.Diagnostics;
using NBitcoin;

namespace WeakKeyScanner;

public readonly record struct ScanStats(long Candidates, long Addresses, int Hits, TimeSpan Elapsed);

/// <summary>
/// Runs candidates through the pipeline: derive addresses, test each against the
/// funded oracle, enrich hits, write findings, report progress. Never signs.
/// </summary>
public sealed class Scanner
{
    private readonly IFundedOracle _oracle;
    private readonly EsploraClient? _enrich;
    private readonly FindingsWriter _findings;
    private readonly Network _network;
    private readonly int _indices;
    private readonly long _progressEvery;

    public Scanner(IFundedOracle oracle, EsploraClient? enrich, FindingsWriter findings,
                   Network network, int indices = 1, long progressEvery = 2000)
    {
        _oracle = oracle;
        _enrich = enrich;
        _findings = findings;
        _network = network;
        _indices = indices;
        _progressEvery = progressEvery;
    }

    public async Task<ScanStats> RunAsync(IEnumerable<WeakKey> keys, string patternType, CancellationToken ct = default)
    {
        long candidates = 0, addresses = 0;
        int hits = 0;
        var sw = Stopwatch.StartNew();

        foreach (var wk in keys)
        {
            ct.ThrowIfCancellationRequested();
            candidates++;

            foreach (var d in Bip39Deriver.Derive(wk.Mnemonic, _network, _indices))
            {
                addresses++;
                var info = await _oracle.LookupAsync(d.Address, ct);
                if (info is null) continue;

                var f = info.Value;
                if (f.TxCount == 0 && _enrich is not null) // offline hit: fill in details
                {
                    try { f = await _enrich.CheckAsync(d.Address, ct); } catch { /* keep marker */ }
                }

                hits++;
                _findings.Write(new Finding(
                    patternType, wk.Mnemonic, d.Kind, d.Path, "bitcoin", d.Address,
                    f.EverFunded, f.TxCount, f.TotalReceivedSats, f.BalanceSats,
                    Explorers.Address(d.Address)));

                Console.WriteLine($"  >>> HIT  {d.Kind}  {d.Address}");
                Console.WriteLine($"      key : {wk.Mnemonic}");
                Console.WriteLine($"      {Format.Activity(f)}");
                Console.WriteLine($"      {Explorers.Address(d.Address)}");
            }

            if (_progressEvery > 0 && candidates % _progressEvery == 0)
                ReportProgress(candidates, addresses, hits, sw);
        }

        sw.Stop();
        return new ScanStats(candidates, addresses, hits, sw.Elapsed);
    }

    private static void ReportProgress(long candidates, long addresses, int hits, Stopwatch sw)
    {
        double rate = candidates / Math.Max(0.001, sw.Elapsed.TotalSeconds);
        Console.WriteLine($"... {candidates:N0} candidates | {addresses:N0} addresses | {hits} hits | {rate:N0} cand/s");
    }
}
