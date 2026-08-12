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
        int wordCount = 12, from = 0, to = -1, words = 2048, indices = 1, width = 24;
        bool firstOnly = false, brain = false, raw = false, append = false;
        bool? f1 = null, f2 = null;   // null = "not explicitly set"; defaults to both under --raw
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
                case "--raw": raw = true; break;
                case "--width": width = int.Parse(args[++i]); break;
                case "--f1": f1 = true; break;
                case "--f2": f2 = true; break;
                case "--wordlist": wordlist = args[++i]; break;
                case "--append": append = true; break;
                case "--out": outFile = args[++i]; break;
                default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
            }
        }

        int toWord = to >= 0 ? to : Math.Min(2048, from + words);
        var full = Path.GetFullPath(outFile);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var net = Network.Main;
        long n = 0;

        // Under --raw, emit both raw families unless the caller named one explicitly.
        bool doF1 = raw && (f1 ?? !(f2 ?? false));
        bool doF2 = raw && (f2 ?? !(f1 ?? false));

        string target = brain ? "brainwallet SHA256(pw)"
            : raw ? $"raw target-K [{(doF1 ? $"F1 periodic fills w<={width}" : "")}{(doF1 && doF2 ? " + " : "")}{(doF2 ? "F2 hex-word/byte fills" : "")}]"
            : $"mnemonic {pattern}/{wordCount} words[{from},{toWord}) indices={indices} completions={(firstOnly ? "first" : "all")}";

        Console.WriteLine("ill-advised-private-keys — emit weak-key candidate set");
        Console.WriteLine($"target : {target}");
        Console.WriteLine($"out    : {full}{(append ? " (append)" : "")}");

        // Append mode lets several families accumulate into one weakset; the header is
        // written only when creating (truncating) the file so appends stay column-clean.
        bool writeHeader = !(append && File.Exists(full));
        using var w = new StreamWriter(full, append: append);
        if (writeHeader) w.WriteLine("# address\tweakkey\tkind\tpath");

        if (brain)
        {
            if (wordlist is null || !File.Exists(wordlist)) { Console.Error.WriteLine("emit --brain requires --wordlist FILE"); return 2; }
            foreach (var pw in File.ReadLines(wordlist).Where(l => l.Length > 0))
                foreach (var d in BrainwalletDeriver.Derive(pw, net))
                { w.WriteLine($"{d.Address}\t{pw}\t{d.Kind}\t{d.Path}"); n++; }
        }
        else if (raw)
        {
            // Target K: a 256-bit scalar imported directly as a private key (no PBKDF2),
            // one EC multiply per candidate. RawKeyDeriver yields 0 addresses for scalars
            // that aren't valid keys (0 or >= curve order), so those fall out silently.
            //
            // The per-candidate EC work (two pubkeys + four address encodings) is the cost
            // and is independent per candidate, so it fans out across cores. The generator's
            // MoveNext (dedup HashSet + bit fill) stays serialized by the partitioner — cheap
            // and keeps the dedup correct; only the crypto runs in parallel. Weakset order is
            // irrelevant (nodewalk loads it into a dict), so unordered output is fine.
            long cands = 0, addrs = 0;
            var families = new List<IEnumerable<RawCandidate>>();
            if (doF1) families.Add(RawKeyGenerator.PeriodicFills(width));
            if (doF2) families.Add(RawKeyGenerator.HexWordFills());

            var writeLock = new object();
            foreach (var fam in families)
            {
                System.Threading.Tasks.Parallel.ForEach(
                    fam,
                    () => new System.Text.StringBuilder(1 << 16),
                    (c, _, local) =>
                    {
                        foreach (var d in RawKeyDeriver.Derive(c.Key, net))
                        {
                            local.Append(d.Address).Append('\t').Append(c.Label).Append('\t')
                                 .Append(d.Kind).Append('\t').Append(d.Path).Append('\n');
                            System.Threading.Interlocked.Increment(ref addrs);
                        }
                        long done = System.Threading.Interlocked.Increment(ref cands);
                        if (local.Length > 1 << 15)
                        {
                            lock (writeLock) w.Write(local.ToString());
                            local.Clear();
                        }
                        if ((done & 0xFFFFF) == 0)
                            Console.WriteLine($"... {done:N0} candidates, {System.Threading.Interlocked.Read(ref addrs):N0} addresses");
                        return local;
                    },
                    local => { if (local.Length > 0) lock (writeLock) w.Write(local.ToString()); });
            }
            n = addrs;
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
