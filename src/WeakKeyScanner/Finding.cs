using System.Globalization;
using System.Text.Json;

namespace WeakKeyScanner;

/// <summary>
/// One compromised-key record. Self-contained: the weak key, where it derives to,
/// the on-chain funding facts, and a human-checkable public link.
/// </summary>
public sealed record Finding(
    string PatternType,
    string WeakKey,
    string Kind,
    string Path,
    string Chain,
    string Address,
    bool EverFunded,
    int TxCount,
    long TotalReceivedSats,
    long BalanceSats,
    string ExplorerUrl);

public static class Explorers
{
    public static string Address(string address) => $"https://mempool.space/address/{address}";
}

public static class Format
{
    public static string Btc(long sats) =>
        (sats / 100_000_000m).ToString("0.########", CultureInfo.InvariantCulture) + " BTC";

    /// <summary>One-line human summary of the funding activity.</summary>
    public static string Activity(FundingInfo f)
    {
        if (!f.EverFunded) return "no on-chain history";
        var note = f.BalanceSats > 0 ? "LIVE BALANCE" : "funded then drained";
        return $"{f.TxCount} txs · received {Btc(f.TotalReceivedSats)} · balance {Btc(f.BalanceSats)} · {note}";
    }
}

/// <summary>Appends findings to a local JSONL file (one JSON object per line).</summary>
public sealed class FindingsWriter : IDisposable
{
    private static readonly JsonSerializerOptions Options = new() { WriteIndented = false };
    private readonly StreamWriter _writer;
    public string Path { get; }

    public FindingsWriter(string path)
    {
        Path = System.IO.Path.GetFullPath(path);
        Directory.CreateDirectory(System.IO.Path.GetDirectoryName(Path)!);
        _writer = new StreamWriter(Path, append: false) { AutoFlush = true };
    }

    public void Write(Finding f) => _writer.WriteLine(JsonSerializer.Serialize(f, Options));

    public void Dispose() => _writer.Dispose();
}
