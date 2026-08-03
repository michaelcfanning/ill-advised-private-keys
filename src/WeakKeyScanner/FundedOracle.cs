namespace WeakKeyScanner;

/// <summary>Returns funding facts for an address, or null if the address is not funded.</summary>
public interface IFundedOracle
{
    Task<FundingInfo?> LookupAsync(string address, CancellationToken ct = default);
    string Describe();
}

/// <summary>
/// Primary scan path: an in-memory set of ever-funded addresses. Detection is a
/// local membership test — no network, no rate limit, nothing leaves the machine.
/// A hit returns a marker; details are filled by a separate enrichment call.
/// </summary>
public sealed class OfflineSetOracle : IFundedOracle
{
    private readonly HashSet<string> _set;
    public int Count => _set.Count;

    public OfflineSetOracle(IEnumerable<string> addresses)
        => _set = new HashSet<string>(addresses, StringComparer.Ordinal);

    public static OfflineSetOracle FromFile(string path)
    {
        var addresses = File.ReadLines(path)
            .Select(l => l.Trim())
            .Where(l => l.Length > 0 && l[0] != '#');
        return new OfflineSetOracle(addresses);
    }

    public Task<FundingInfo?> LookupAsync(string address, CancellationToken ct = default)
        => Task.FromResult(_set.Contains(address) ? new FundingInfo(true, 0, 0, 0) : (FundingInfo?)null);

    public string Describe() => $"offline set ({_set.Count:N0} addresses)";
}

/// <summary>
/// Test-only oracle that checks each address against a public Esplora instance.
/// Rate-limited and leaks the candidate set to the operator — never use for a full
/// scan; only for small bounded runs and reconnaissance.
/// </summary>
public sealed class EsploraApiOracle : IFundedOracle
{
    private readonly EsploraClient _client;
    private readonly int _delayMs;

    public EsploraApiOracle(EsploraClient client, int delayMs = 250)
    {
        _client = client;
        _delayMs = delayMs;
    }

    public async Task<FundingInfo?> LookupAsync(string address, CancellationToken ct = default)
    {
        var f = await _client.CheckAsync(address, ct);
        if (_delayMs > 0) await Task.Delay(_delayMs, ct);
        return f.EverFunded ? f : (FundingInfo?)null;
    }

    public string Describe() => "esplora API (rate-limited; test/recon use only)";
}
