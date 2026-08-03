namespace WeakKeyScanner;

/// <summary>Returns funding facts for an address, or null if the address is not funded.</summary>
public interface IFundedOracle
{
    Task<FundingInfo?> LookupAsync(string address, CancellationToken ct = default);
    string Describe();
}

/// <summary>
/// Primary scan path: an in-memory set of funded addresses. Detection is a local
/// membership test — no network, no rate limit, nothing leaves the machine.
///
/// Addresses are stored as 64-bit FNV-1a hashes so tens of millions fit in ~1 GB.
/// A 64-bit collision over a query is astronomically unlikely, and any hit is
/// re-verified against live chain data during enrichment, so a stray collision is
/// dropped rather than reported. Accepts plain address-per-line files or TSV
/// (address&lt;tab&gt;balance, e.g. Blockchair/Loyce lists) — the first token is used.
/// </summary>
public sealed class OfflineSetOracle : IFundedOracle
{
    private readonly HashSet<ulong> _hashes;
    public int Count => _hashes.Count;

    private OfflineSetOracle(HashSet<ulong> hashes) => _hashes = hashes;

    public static OfflineSetOracle FromFile(string path)
    {
        var hashes = new HashSet<ulong>();
        foreach (var line in File.ReadLines(path))
        {
            var token = FirstToken(line);
            if (token.Length == 0 || token[0] == '#') continue;
            hashes.Add(Hash(token));
        }
        return new OfflineSetOracle(hashes);
    }

    public Task<FundingInfo?> LookupAsync(string address, CancellationToken ct = default)
        => Task.FromResult(_hashes.Contains(Hash(address)) ? new FundingInfo(true, 0, 0, 0) : (FundingInfo?)null);

    public string Describe() => $"offline set ({_hashes.Count:N0} address hashes, 64-bit)";

    private static string FirstToken(string line)
    {
        var span = line.AsSpan().Trim();
        int cut = span.IndexOfAny(' ', '\t');
        return (cut < 0 ? span : span[..cut]).ToString();
    }

    private static ulong Hash(string s)
    {
        ulong h = 1469598103934665603UL; // FNV-1a offset basis
        foreach (char c in s) { h ^= (byte)c; h *= 1099511628211UL; }
        return h;
    }
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
