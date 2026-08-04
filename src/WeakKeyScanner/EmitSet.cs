using NBitcoin;

namespace WeakKeyScanner;

/// <summary>
/// emit — serialize enumerated weak-key candidate addresses to a TSV so the
/// ever-funded walk (<see cref="NodeWalk"/>) or <see cref="BlockchairScan"/> can
/// intersect them against chain data. This is the producer the pipeline was missing:
/// <c>scan</c>/<c>brainscan</c> derive addresses only in memory, so nothing wrote the
/// candidate set out. Columns: <c>address\tweakkey\tkind\tpath</c> (the first token is
/// the address, so the file is also valid as a plain <c>--weakset</c> for bcscan).
///
/// Derivation is CPU-bound (PBKDF2 + EC); this runs single-threaded for simplicity, so
/// emit bounded ranges. The mnemonic families are the primary target (far smaller than
/// the 128M brainwallet space).
/// </summary>
public static class EmitSet
{
    public static int Run(string[] args)
    {
        string pattern = "repeat";
        int wordCount = 12, from = 0, to = -1, words = 2048, indices = 1;
        bool firstOnly = false, brain = false;
        string? wordlist = null;
        string outFile = Path.Combine(AppContext.BaseDirectory, "out", "weakset.tsv");

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--pattern": pattern = args[++i]; break;
                case "--wordcount": wordCount = int.Parse(args[++i]); break;
                case "--from": from = int.Parse(args[++i]); break;
                case "--to": to = int.Parse(args[++i]); break;
                case "--words": words = int.Parse(args[++i]); break;
                case "--completions": firstOnly = args[++i] == "first"; break;
                case "--indices": indices = int.Parse(args[++i]); break;
                case "--brain": brain = true; break;
                case "--wordlist": wordlist = args[++i]; break;
                case "--out": outFile = args[++i]; break;
                default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
            }
        }

        int toWord = to >= 0 ? to : Math.Min(2048, from + words);
        var full = Path.GetFullPath(outFile);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var net = Network.Main;
        long n = 0;

        Console.WriteLine("ill-advised-private-keys — emit weak-key candidate set");
        Console.WriteLine($"target : {(brain ? "brainwallet SHA256(pw)" : $"mnemonic {pattern}/{wordCount} words[{from},{toWord}) indices={indices} completions={(firstOnly ? "first" : "all")}")}");
        Console.WriteLine($"out    : {full}");

        using var w = new StreamWriter(full, append: false);
        w.WriteLine("# address\tweakkey\tkind\tpath");

        if (brain)
        {
            if (wordlist is null || !File.Exists(wordlist)) { Console.Error.WriteLine("emit --brain requires --wordlist FILE"); return 2; }
            foreach (var pw in File.ReadLines(wordlist).Where(l => l.Length > 0))
                foreach (var d in BrainwalletDeriver.Derive(pw, net))
                { w.WriteLine($"{d.Address}\t{pw}\t{d.Kind}\t{d.Path}"); n++; }
        }
        else
        {
            var spec = PatternSpec.Parse(pattern);
            foreach (var wk in PatternGenerator.All(spec, wordCount, from, toWord, firstOnly))
                foreach (var d in Bip39Deriver.Derive(wk.Mnemonic, net, indices))
                { w.WriteLine($"{d.Address}\t{wk.Mnemonic}\t{d.Kind}\t{d.Path}"); n++; }
        }

        Console.WriteLine($"emitted {n:N0} addresses");
        return 0;
    }
}
