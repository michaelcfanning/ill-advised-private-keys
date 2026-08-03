using System.Text.Json;

namespace WeakKeyScanner;

/// <summary>Quick funding facts for an address (chain_stats only).</summary>
public readonly record struct FundingInfo(
    bool EverFunded, int TxCount, long TotalReceivedSats, long BalanceSats);

/// <summary>
/// Deep activity for a compromised address: funders (money in), sweepers (money
/// out — the drainer set), and the first/last on-chain timestamps.
/// </summary>
public readonly record struct AddressActivity(
    bool EverFunded, int TxCount, long TotalReceivedSats, long BalanceSats,
    DateTimeOffset? FirstSeen, DateTimeOffset? LastSeen,
    int InboundTxs, int OutboundTxs,
    IReadOnlyList<string> Funders, IReadOnlyList<string> Sweepers)
{
    public FundingInfo ToFundingInfo() => new(EverFunded, TxCount, TotalReceivedSats, BalanceSats);
}

/// <summary>
/// Read-only oracle over a public Esplora instance (blockstream.info / mempool.space).
/// Queries address stats and history only. No keys, no writes.
/// </summary>
public sealed class EsploraClient : IDisposable
{
    private readonly HttpClient _http;
    private readonly int _pageDelayMs;

    public EsploraClient(string baseUrl = "https://blockstream.info/api/", int pageDelayMs = 150)
    {
        _http = new HttpClient { BaseAddress = new Uri(baseUrl) };
        _http.DefaultRequestHeaders.UserAgent.ParseAdd(
            "ill-advised-private-keys/0.1 (whitehat vulnerability research)");
        _pageDelayMs = pageDelayMs;
    }

    /// <summary>Fast funded/received/balance from chain_stats — one request.</summary>
    public async Task<FundingInfo> CheckAsync(string address, CancellationToken ct = default)
    {
        using var doc = await GetJsonAsync($"address/{address}", ct);
        var cs = doc.RootElement.GetProperty("chain_stats");
        long funded = cs.GetProperty("funded_txo_sum").GetInt64();
        long spent = cs.GetProperty("spent_txo_sum").GetInt64();
        int txCount = cs.GetProperty("tx_count").GetInt32();
        return new FundingInfo(txCount > 0 || funded > 0, txCount, funded, funded - spent);
    }

    /// <summary>
    /// Walk the full confirmed history and derive funders, sweepers, and dates.
    /// Paginated (25 tx/page); used only on the few addresses that are hits.
    /// </summary>
    public async Task<AddressActivity> AnalyzeAsync(string address, CancellationToken ct = default)
    {
        long received = 0, spent = 0;
        int txCount = 0, inbound = 0, outbound = 0;
        long? first = null, last = null;
        var funders = new HashSet<string>(StringComparer.Ordinal);
        var sweepers = new HashSet<string>(StringComparer.Ordinal);

        string? lastTxid = null;
        while (true)
        {
            var url = lastTxid is null
                ? $"address/{address}/txs/chain"
                : $"address/{address}/txs/chain/{lastTxid}";
            using var doc = await GetJsonAsync(url, ct);
            var page = doc.RootElement;
            int n = page.GetArrayLength();
            if (n == 0) break;

            foreach (var tx in page.EnumerateArray())
            {
                txCount++;
                var status = tx.GetProperty("status");
                if (status.GetProperty("confirmed").GetBoolean()
                    && status.TryGetProperty("block_time", out var bt))
                {
                    long t = bt.GetInt64();
                    first = first is null ? t : Math.Min(first.Value, t);
                    last = last is null ? t : Math.Max(last.Value, t);
                }

                bool isInbound = false, isOutbound = false;
                foreach (var vout in tx.GetProperty("vout").EnumerateArray())
                {
                    if (AddressOf(vout) == address)
                    {
                        isInbound = true;
                        received += vout.GetProperty("value").GetInt64();
                    }
                }
                foreach (var vin in tx.GetProperty("vin").EnumerateArray())
                {
                    if (!vin.TryGetProperty("prevout", out var prevout) || prevout.ValueKind == JsonValueKind.Null)
                        continue;
                    if (AddressOf(prevout) == address)
                    {
                        isOutbound = true;
                        spent += prevout.GetProperty("value").GetInt64();
                    }
                }

                if (isInbound)
                {
                    inbound++;
                    foreach (var vin in tx.GetProperty("vin").EnumerateArray())
                    {
                        if (!vin.TryGetProperty("prevout", out var prevout) || prevout.ValueKind == JsonValueKind.Null)
                            continue;
                        var a = AddressOf(prevout);
                        if (a is not null && a != address) funders.Add(a);
                    }
                }
                if (isOutbound)
                {
                    outbound++;
                    foreach (var vout in tx.GetProperty("vout").EnumerateArray())
                    {
                        var a = AddressOf(vout);
                        if (a is not null && a != address) sweepers.Add(a);
                    }
                }
                lastTxid = tx.GetProperty("txid").GetString();
            }

            if (n < 25) break;
            if (_pageDelayMs > 0) await Task.Delay(_pageDelayMs, ct);
        }

        return new AddressActivity(
            EverFunded: received > 0 || txCount > 0,
            TxCount: txCount, TotalReceivedSats: received, BalanceSats: received - spent,
            FirstSeen: first is null ? null : DateTimeOffset.FromUnixTimeSeconds(first.Value),
            LastSeen: last is null ? null : DateTimeOffset.FromUnixTimeSeconds(last.Value),
            InboundTxs: inbound, OutboundTxs: outbound,
            Funders: funders.ToArray(), Sweepers: sweepers.ToArray());
    }

    private static string? AddressOf(JsonElement scriptOwner) =>
        scriptOwner.TryGetProperty("scriptpubkey_address", out var a) ? a.GetString() : null;

    private async Task<JsonDocument> GetJsonAsync(string url, CancellationToken ct)
    {
        using var resp = await _http.GetAsync(url, ct);
        resp.EnsureSuccessStatusCode();
        await using var stream = await resp.Content.ReadAsStreamAsync(ct);
        return await JsonDocument.ParseAsync(stream, cancellationToken: ct);
    }

    public void Dispose() => _http.Dispose();
}
