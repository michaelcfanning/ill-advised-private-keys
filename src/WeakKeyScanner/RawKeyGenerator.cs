using System.Text;

namespace WeakKeyScanner;

/// <summary>A raw 256-bit key candidate (target K) and the rule that produced it.</summary>
public readonly record struct RawCandidate(byte[] Key, string Label);

/// <summary>
/// Enumerates the raw-key (target K) families from PATTERNS.md that have never been
/// systematically scanned on the private-key side:
///
///   F1 — Periodic fills: a unit of width w bits repeated to fill 256 bits, truncating
///        the final repeat. Done at the *bit* level so non-byte-aligned periods
///        (3, 5, 6 …) — the ones a byte-oriented eyeball check misses — are included.
///        Enumerated for every unit value at every width 1..maxWidth, deduped by key
///        (a 0xAA fill arises from w = 2, 4, 8 alike).
///
///   F2 — Hex-word fills: programmer "magic" constants (deadbeef, cafebabe, …), their
///        reversals, and pairwise concatenations, each repeated to 32 bytes; plus the
///        256 single-byte fills (0x00…0xFF).
///
/// Costs: F1 has sum_{w=1..W} 2^w units before dedup (~131k at W=16, ~33.5M at W=24);
/// each candidate is one cheap EC multiply, so even W=24 is tractable for target K.
/// </summary>
public static class RawKeyGenerator
{
    public static IEnumerable<RawCandidate> PeriodicFills(int maxWidth)
    {
        var seen = new HashSet<string>();
        for (int w = 1; w <= maxWidth; w++)
        {
            long units = 1L << w;
            for (int u = 0; u < units; u++)
            {
                var key = FillBits(u, w);
                if (seen.Add(Convert.ToHexString(key)))
                    yield return new RawCandidate(key, $"fill:w={w}:u=0x{u:x}");
            }
        }
    }

    public static IEnumerable<RawCandidate> HexWordFills()
    {
        var seen = new HashSet<string>();

        // Single-byte fills 0x00..0xFF (these are also F1 units of width 8, but cheap to
        // include explicitly with a readable label).
        for (int b = 0; b <= 0xff; b++)
        {
            var key = new byte[32];
            Array.Fill(key, (byte)b);
            if (seen.Add(Convert.ToHexString(key)))
                yield return new RawCandidate(key, $"bytefill:0x{b:x2}");
        }

        string[] words =
        {
            "deadbeef", "cafebabe", "feedface", "baadf00d", "deadc0de", "8badf00d",
            "decafbad", "badc0ffe", "feedbeef", "deadfa11", "defec8ed", "c0ffee",
            "facefeed", "0ddba11", "abadcafe", "d15ea5e", "b16b00b5", "cafed00d",
        };

        var forms = new List<string>();
        foreach (var x in words) { forms.Add(x); forms.Add(Reverse(x)); }
        foreach (var x in words)
            foreach (var y in words)
                forms.Add(x + y);

        foreach (var f in forms)
        {
            var key = HexFill(f);
            if (key is not null && seen.Add(Convert.ToHexString(key)))
                yield return new RawCandidate(key, $"hexfill:{f}");
        }
    }

    private static string Reverse(string s)
    {
        var a = s.ToCharArray();
        Array.Reverse(a);
        return new string(a);
    }

    /// <summary>Repeat the w-bit unit across 256 bits, big-endian within each unit.</summary>
    private static byte[] FillBits(int unit, int width)
    {
        var key = new byte[32];
        int pos = 0;
        while (pos < 256)
            for (int b = width - 1; b >= 0 && pos < 256; b--, pos++)
                if (((unit >> b) & 1) == 1)
                    key[pos / 8] |= (byte)(1 << (7 - (pos % 8)));
        return key;
    }

    /// <summary>Repeat a hex word to exactly 64 hex chars (32 bytes), then parse.</summary>
    private static byte[]? HexFill(string word)
    {
        if (word.Length == 0) return null;
        var sb = new StringBuilder(64);
        while (sb.Length < 64) sb.Append(word);
        try { return Convert.FromHexString(sb.ToString(0, 64)); }
        catch { return null; }
    }
}
