using System.Text.Json;

namespace WeakKeyScanner;

/// <summary>Read-only funding facts for an address, from public chain data.</summary>
public readonly record struct FundingInfo(
    bool EverFunded, int TxCount, long TotalReceivedSats, long BalanceSats);

/// <summary>
/// Read-only oracle over a public Esplora instance (blockstream.info / mempool.space).
/// Queries address chain stats only. No keys, no writes.
/// </summary>
public sealed class EsploraClient : IDisposable
{
    private readonly HttpClient _http;

    public EsploraClient(string baseUrl = "https://blockstream.info/api/")
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ill-advised-private-keys/0.1 (whitehat vulnerability research)");
    }

    public async Task<FundingInfo> CheckAsync(string address, CancellationToken ct = default)
    {
        using var resp = await _http.GetAsync($"address/{address}", ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        using var doc = await JsonDocument.ParseAsync(stream, cancellationToken: ct);

        var cs = doc.RootElement.GetProperty("chain_stats");
        long funded = cs.GetProperty("funded_txo_sum").GetInt64();
        long spent = cs.GetProperty("spent_txo_sum").GetInt64();
        int txCount = cs.GetProperty("tx_count").GetInt32();

        return new FundingInfo(
            EverFunded: txCount > 0 || funded > 0,
            TxCount: txCount,
            TotalReceivedSats: funded,
            BalanceSats: funded - spent);
    }

    public void Dispose() => _http.Dispose();
}
