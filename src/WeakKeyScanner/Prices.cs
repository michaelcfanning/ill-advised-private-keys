using System.Globalization;

namespace WeakKeyScanner;

/// <summary>
/// Daily BTC/USD close, for valuing on-chain flows at transaction time (the
/// USD-at-transaction-time basis in ECONOMICS.md). Loads a "date,usd" CSV
/// (data/btc_usd_daily.csv). A lookup for a date with no exact row falls back to the
/// nearest prior day, so a gap in the series still resolves. Pre-market dates
/// (2009–2010) carry ~0, which is correct — those coins had no USD market.
/// </summary>
public sealed class Prices
{
    private readonly SortedList<string, decimal> _byDate = new(StringComparer.Ordinal);
    public int Count => _byDate.Count;

    public static Prices Load(string path)
    {
        var p = new Prices();
        foreach (var line in File.ReadLines(path))
        {
            var s = line.AsSpan().Trim();
            if (s.Length == 0 || s[0] == '#') continue;
            int comma = s.IndexOf(',');
            if (comma < 0) continue;
            var date = s[..comma].ToString();
            if (date == "date") continue; // header
            if (decimal.TryParse(s[(comma + 1)..], NumberStyles.Any, CultureInfo.InvariantCulture, out var usd))
                p._byDate[date] = usd;
        }
        return p;
    }

    /// <summary>USD close for a yyyy-MM-dd date, or the nearest prior day; null if none.</summary>
    public decimal? Usd(string? date)
    {
        if (date is null || _byDate.Count == 0) return null;
        if (_byDate.TryGetValue(date, out var v)) return v;
        decimal? prior = null;                       // last key <= date (yyyy-MM-dd sorts chronologically)
        foreach (var kv in _byDate)
        {
            if (string.CompareOrdinal(kv.Key, date) <= 0) prior = kv.Value;
            else break;
        }
        return prior;
    }
}
