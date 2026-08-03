using NBitcoin;

namespace WeakKeyScanner;

/// <summary>A generated weak mnemonic and the pattern parameters that produced it.</summary>
public readonly record struct WeakKey(int WordIndex, string Word, int WordCount, int FreeBits, string Mnemonic);

/// <summary>
/// Enumerates the "repeated word" family: the first (n-1) words are one repeated
/// wordlist entry, and the final word is whatever makes the BIP-39 checksum valid.
///
/// For a repeated word the first (n-1) words fix (n-1)*11 entropy bits; the
/// remaining entropy bits are free and each free-bit assignment yields exactly one
/// valid final word. So each repeated word produces 2^freeBits valid mnemonics:
/// 128 for 12 words (7 free bits), 8 for 24 words (3 free bits).
/// </summary>
public static class RepeatedWordGenerator
{
    private static readonly Wordlist English = Wordlist.English;

    public static IEnumerable<WeakKey> ForWord(int wordIndex, int wordCount)
    {
        if (wordCount is not (12 or 24))
            throw new ArgumentException("wordCount must be 12 or 24", nameof(wordCount));

        int entropyBits = wordCount == 12 ? 128 : 256;
        int repeats = wordCount - 1;
        int freeBitCount = entropyBits - repeats * 11; // 7 for 12 words, 3 for 24
        int freeMax = 1 << freeBitCount;

        for (int free = 0; free < freeMax; free++)
        {
            var bits = new bool[entropyBits];
            int pos = 0;
            for (int r = 0; r < repeats; r++)
                pos = WriteBits(bits, pos, wordIndex, 11);
            WriteBits(bits, pos, free, freeBitCount);

            var mnemonic = new Mnemonic(English, ToBytes(bits));
            yield return new WeakKey(wordIndex, mnemonic.Words[0], wordCount, free, mnemonic.ToString());
        }
    }

    /// <summary>Every repeated-word mnemonic for word indices [fromWord, toWord).</summary>
    public static IEnumerable<WeakKey> All(int wordCount, int fromWord = 0, int toWord = 2048, bool firstCompletionOnly = false)
    {
        for (int i = fromWord; i < toWord; i++)
        {
            foreach (var wk in ForWord(i, wordCount))
            {
                yield return wk;
                if (firstCompletionOnly) break; // free-bits == 0: the "lazy" completion
            }
        }
    }

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
