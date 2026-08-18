using System.Text;

namespace WeakKeyScanner;

/// <summary>
/// The raw target-K families from PATTERNS.md beyond F1/F2: counters (F3), sparse/dense
/// keys (F4, incl. the privkey=1 / small-integer / puzzle space), ASCII payloads (F5),
/// structured decimal (F6), and nothing-up-my-sleeve constants (F8). All target K — one EC
/// multiply per candidate — so even a few million is cheap. Each candidate is a 32-byte
/// big-endian key; RawKeyDeriver silently drops any that are 0 or ≥ the curve order.
///
/// F4 is also a positive control: privkey=1 and the low Bitcoin "puzzle" integers are
/// long-swept, so the pipeline should light up on them.
/// </summary>
public static class RawKeyFamilies
{
    /// <summary>F3 — byte counters (start, ±stride) filling 32 bytes, plus the nibble run.</summary>
    public static IEnumerable<RawCandidate> Counters()
    {
        var seen = new HashSet<string>();
        for (int start = 0; start < 256; start++)
            for (int stride = 1; stride < 256; stride++)
                foreach (int dir in Dirs)
                {
                    var key = new byte[32];
                    int v = start;
                    for (int i = 0; i < 32; i++) { key[i] = (byte)(v & 0xff); v += dir * stride; }
                    if (seen.Add(Convert.ToHexString(key)))
                        yield return new RawCandidate(key, $"counter:start=0x{start:x2}:stride={dir * stride}");
                }
        // ascending-nibble fill: 0x01 0x23 0x45 ... (the "0123456789abcdef…" idea, byte-packed)
        var nib = new byte[32];
        for (int i = 0; i < 32; i++) nib[i] = (byte)(((2 * i & 0xf) << 4) | ((2 * i + 1) & 0xf));
        yield return new RawCandidate(nib, "nibble:ascending");
    }

    /// <summary>F4 — single-bit, two-bit, small integers 1..2^bits, and dense complements.</summary>
    public static IEnumerable<RawCandidate> SparseDense(int smallIntBits = 20)
    {
        for (int k = 0; k < 256; k++)                 // single bit set (2^k); k=0 is privkey=1
            yield return new RawCandidate(Bit(k), $"bit:2^{k}");

        for (int a = 0; a < 256; a++)                 // two bits set
            for (int b = a + 1; b < 256; b++)
            {
                var key = Bit(a);
                key[31 - b / 8] |= (byte)(1 << (b % 8));
                yield return new RawCandidate(key, $"bits:2^{a}+2^{b}");
            }

        long max = 1L << smallIntBits;                // small integers (puzzle space)
        for (long n = 1; n < max; n++)
        {
            var key = new byte[32];
            for (int i = 0; i < 8; i++) key[31 - i] = (byte)(n >> (8 * i));
            yield return new RawCandidate(key, $"int:{n}");
        }

        for (int k = 0; k < 256; k++)                 // dense: all ones except one bit
        {
            var key = new byte[32];
            Array.Fill(key, (byte)0xff);
            key[31 - k / 8] &= (byte)~(1 << (k % 8));
            yield return new RawCandidate(key, $"dense:~2^{k}");
        }
    }

    /// <summary>F5 — a token written directly into the key bytes (not hashed): repeat / zero / space pad.</summary>
    public static IEnumerable<RawCandidate> AsciiPayloads(IEnumerable<string> words)
    {
        var seen = new HashSet<string>();
        foreach (var w in words)
        {
            var raw = Encoding.ASCII.GetBytes(w);
            if (raw.Length == 0 || raw.Length > 32) continue;
            foreach (var mode in AsciiModes)
            {
                var key = new byte[32];
                if (mode == "sp") Array.Fill(key, (byte)0x20);
                if (mode == "rep") for (int i = 0; i < 32; i++) key[i] = raw[i % raw.Length];
                else Array.Copy(raw, key, raw.Length);   // z / sp: token at front, padded
                if (seen.Add(Convert.ToHexString(key)))
                    yield return new RawCandidate(key, $"ascii:{mode}:{w}");
            }
        }
    }

    /// <summary>F6 — dates (YYYYMMDD), "12345678", and decimal digits of π/e, each filling the key.</summary>
    public static IEnumerable<RawCandidate> StructuredDecimal()
    {
        var seen = new HashSet<string>();
        for (int y = 2008; y <= 2026; y++)
            for (int m = 1; m <= 12; m++)
                for (int d = 1; d <= 28; d++)
                {
                    var key = FillAscii($"{y:0000}{m:00}{d:00}");
                    if (seen.Add(Convert.ToHexString(key))) yield return new RawCandidate(key, $"date:{y:0000}{m:00}{d:00}");
                }
        foreach (var (s, label) in DecimalConstants)
            yield return new RawCandidate(FillAscii(s), $"dec:{label}");
    }

    /// <summary>F8 — published "nothing-up-my-sleeve" constants a user might treat as random.</summary>
    public static IEnumerable<RawCandidate> NumsConstants()
    {
        // SHA-256 initial hash values H0..H7 concatenated (32 bytes).
        uint[] h = { 0x6a09e667, 0xbb67ae85, 0x3c6ef372, 0xa54ff53a, 0x510e527f, 0x9b05688c, 0x1f83d9ab, 0x5be0cd19 };
        var hk = new byte[32];
        for (int i = 0; i < 8; i++) { hk[i * 4] = (byte)(h[i] >> 24); hk[i * 4 + 1] = (byte)(h[i] >> 16); hk[i * 4 + 2] = (byte)(h[i] >> 8); hk[i * 4 + 3] = (byte)h[i]; }
        yield return new RawCandidate(hk, "nums:sha256-H");

        foreach (var (hex, label) in HexConstants)
        {
            byte[] key;
            try { key = Convert.FromHexString(hex); } catch { continue; }
            if (key.Length == 32) yield return new RawCandidate(key, $"nums:{label}");
        }
    }

    // ---- shared bits ----
    private static readonly int[] Dirs = { 1, -1 };
    private static readonly string[] AsciiModes = { "rep", "z", "sp" };

    private static readonly (string, string)[] DecimalConstants =
    {
        ("12345678", "seq8"),
        ("31415926535897932384626433832795028841971693993751", "pi"),
        ("27182818284590452353602874713526624977572470936999", "e"),
        ("14142135623730950488016887242096980785696718753769", "sqrt2"),
    };

    private static readonly (string, string)[] HexConstants =
    {
        // fractional part of pi (the classic NUMS seed) and secp256k1 generator coords.
        ("243f6a8885a308d313198a2e03707344a4093822299f31d0082efa98ec4e6c89", "pi-frac"),
        ("79be667ef9dcbbac55a06295ce870b07029bfcdb2dce28d959f2815b16f81798", "secp-Gx"),
        ("483ada7726a3c4655da4fbfc0e1108a8fd17b448a68554199c47d08ffb10d4b8", "secp-Gy"),
    };

    private static byte[] Bit(int k)
    {
        var key = new byte[32];
        key[31 - k / 8] |= (byte)(1 << (k % 8));
        return key;
    }

    private static byte[] FillAscii(string s)
    {
        var raw = Encoding.ASCII.GetBytes(s);
        var key = new byte[32];
        for (int i = 0; i < 32; i++) key[i] = raw[i % raw.Length];
        return key;
    }

    /// <summary>A modest built-in F5/ASCII token set — the obvious payloads. A full-wordlist
    /// pass is a follow-on; these are the ones a human writes into a key by hand.</summary>
    public static readonly string[] AsciiTokens =
    {
        "password", "bitcoin", "satoshi", "private", "privatekey", "secret", "seedphrase",
        "hello", "test", "admin", "root", "aaaa", "0000", "1234", "qwerty", "letmein",
        "deadbeef", "cafebabe", "abcdefgh", "changeme", "iloveyou", "money", "wallet",
    };
}
