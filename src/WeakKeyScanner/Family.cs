using NBitcoin;

namespace WeakKeyScanner;

/// <summary>
/// Recovers which enumeration family produced a mnemonic, from the mnemonic itself,
/// so findings from a combined (union) walk can be grouped by pattern without the
/// run having to carry a family column. The pattern was applied to the prefix (first
/// n-1) words; the final word is the checksum completion and is ignored. Mirrors
/// <see cref="PatternGenerator"/>: repeat (step 0), forward (+1), backward (-1 ≡ 2047
/// mod 2048), stride:k (+k), else "other".
/// </summary>
public static class Family
{
    public static string Classify(string mnemonic)
    {
        int[] idx;
        try { idx = new Mnemonic(mnemonic, Wordlist.English).Indices; }
        catch { return "other"; }

        int prefix = idx.Length - 1;               // last word is the checksum completion
        if (prefix < 2) return "other";

        bool allSame = true;
        for (int i = 1; i < prefix; i++) if (idx[i] != idx[0]) { allSame = false; break; }
        if (allSame) return "repeat";

        int step = Mod(idx[1] - idx[0]);
        if (step != 0)
        {
            bool arithmetic = true;
            for (int i = 1; i < prefix; i++)
                if (Mod(idx[i] - idx[i - 1]) != step) { arithmetic = false; break; }
            if (arithmetic)
                return step == 1 ? "forward" : step == 2047 ? "backward" : $"stride:{step}";
        }

        // Non-arithmetic periodicity: a period-k word cycle (w0 w1 … repeated). Report
        // the smallest period p in [2, prefix). A constant prefix (p==1) was already
        // caught as "repeat" above, so any p found here is a genuine multi-word cycle.
        for (int p = 2; p < prefix; p++)
        {
            bool periodic = true;
            for (int i = p; i < prefix; i++) if (idx[i] != idx[i - p]) { periodic = false; break; }
            if (periodic) return $"cycle:{p}";
        }
        return "other";
    }

    private static int Mod(int v) { v %= 2048; return v < 0 ? v + 2048 : v; }
}
