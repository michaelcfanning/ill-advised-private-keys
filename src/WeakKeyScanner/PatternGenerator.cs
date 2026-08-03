using NBitcoin;

namespace WeakKeyScanner;

/// <summary>A generated weak mnemonic and the pattern parameters that produced it.</summary>
public readonly record struct WeakKey(int Start, string Word, int WordCount, int FreeBits, string Mnemonic);

/// <summary>
/// A memorable "prefix" pattern: the first (n-1) words follow a rule keyed on a
/// start word, and the final word is whatever makes the BIP-39 checksum valid.
/// WordAt(start, position) returns the wordlist index for each prefix position.
/// </summary>
public sealed record PatternSpec(string Name, Func<int, int, int> WordAt)
{
    public static PatternSpec Parse(string s)
    {
        var parts = s.Split(':');
        return parts[0] switch
        {
            "repeat" => new PatternSpec("repeat", (start, _) => start),
            "forward" => new PatternSpec("forward", (start, pos) => start + pos),
            "backward" => new PatternSpec("backward", (start, pos) => start - pos),
            "stride" => new PatternSpec($"stride{parts[1]}", (start, pos) => start + pos * int.Parse(parts[1])),
            _ => throw new ArgumentException($"unknown pattern: {s} (use repeat|forward|backward|stride:k)")
        };
    }
}

/// <summary>
/// Enumerates prefix patterns. The (n-1) prefix words fix (n-1)*11 entropy bits;
/// the remaining bits are free and each assignment yields one valid final word.
/// So each start produces 2^freeBits mnemonics (128 for 12 words, 8 for 24).
/// The repeated-word family is just the "repeat" pattern (step 0).
/// </summary>
public static class PatternGenerator
{
    private static readonly Wordlist English = Wordlist.English;

    public static IEnumerable<WeakKey> All(PatternSpec spec, int wordCount, int from, int to, bool firstOnly)
    {
        if (wordCount is not (12 or 24))
            throw new ArgumentException("wordCount must be 12 or 24", nameof(wordCount));

        int entropyBits = wordCount == 12 ? 128 : 256;
        int prefix = wordCount - 1;
        int freeBitCount = entropyBits - prefix * 11;
        int freeMax = 1 << freeBitCount;

        for (int start = from; start < to; start++)
        {
            for (int free = 0; free < freeMax; free++)
            {
                var bits = new bool[entropyBits];
                int pos = 0;
                for (int r = 0; r < prefix; r++)
                    pos = WriteBits(bits, pos, Mod2048(spec.WordAt(start, r)), 11);
                WriteBits(bits, pos, free, freeBitCount);

                var mnemonic = new Mnemonic(English, ToBytes(bits));
                yield return new WeakKey(start, mnemonic.Words[0], wordCount, free, mnemonic.ToString());
                if (firstOnly) break;
            }
        }
    }

    private static int Mod2048(int i) { i %= 2048; return i < 0 ? i + 2048 : i; }

    private static int WriteBits(bool[] bits, int pos, int value, int width)
    {
        for (int b = width - 1; b >= 0; b--)
            bits[pos++] = ((value >> b) & 1) == 1;
        return pos;
    }

    private static byte[] ToBytes(bool[] bits)
    {
        var bytes = new byte[bits.Length / 8];
        for (int i = 0; i < bits.Length; i++)
            if (bits[i]) bytes[i / 8] |= (byte)(1 << (7 - (i % 8)));
        return bytes;
    }
}
