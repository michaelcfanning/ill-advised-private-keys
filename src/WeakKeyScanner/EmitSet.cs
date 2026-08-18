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
        bool firstOnly = false, brain = false, raw = false, append = false, variants = false;
        bool f1 = false, f2 = false, f3 = false, f4 = false, f5 = false, f6 = false, f8 = false, allRaw = false;
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
                case "--variants": variants = true; break;
                case "--raw": raw = true; break;
                case "--width": width = int.Parse(args[++i]); break;
                case "--f1": f1 = true; break;
                case "--f2": f2 = true; break;
                case "--f3": f3 = true; break;
                case "--f4": f4 = true; break;
                case "--f5": f5 = true; break;
                case "--f6": f6 = true; break;
                case "--f8": f8 = true; break;
                case "--all-raw": allRaw = true; break;
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

        // Under --raw: default (no family flag) = F1+F2; --all-raw = every family; else the named ones.
        if (allRaw) f1 = f2 = f3 = f4 = f5 = f6 = f8 = true;
        bool anyFam = f1 || f2 || f3 || f4 || f5 || f6 || f8;
        bool doF1 = raw && (f1 || !anyFam), doF2 = raw && (f2 || !anyFam);
        bool doF3 = raw && f3, doF4 = raw && f4, doF5 = raw && f5, doF6 = raw && f6, doF8 = raw && f8;
        string rawFams = string.Join("+", new[] { (doF1, "F1"), (doF2, "F2"), (doF3, "F3"),
            (doF4, "F4"), (doF5, "F5"), (doF6, "F6"), (doF8, "F8") }.Where(x => x.Item1).Select(x => x.Item2));

        string target = brain ? "brainwallet SHA256(pw)"
            : raw ? $"raw target-K [{rawFams}{(doF1 ? $"; F1 w<={width}" : "")}]"
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
            // Brainwallet: privkey = SHA256(pw), one hash + EC per password. Parallel over the
            // streamed wordlist — the partitioner serializes file MoveNext (cheap), the crypto
            // fans out across cores. Order is irrelevant (nodewalk loads into a dict).
            long pws = 0, addrs = 0;
            var writeLock = new object();
            // --variants expands each phrase into the casing/spacing/period forms a human might
            // actually type (brainwallets are exact-string, so the form is the whole game).
            IEnumerable<string> pwSource = variants
                ? File.ReadLines(wordlist).Where(l => l.Trim().Length > 0).SelectMany(Variants)
                : File.ReadLines(wordlist).Where(l => l.Length > 0);
            System.Threading.Tasks.Parallel.ForEach(
                pwSource,
                () => new System.Text.StringBuilder(1 << 16),
                (pw, _, local) =>
                {
                    foreach (var d in BrainwalletDeriver.Derive(pw, net))
                    {
                        local.Append(d.Address).Append('\t').Append(pw).Append('\t')
                             .Append(d.Kind).Append('\t').Append(d.Path).Append('\n');
                        System.Threading.Interlocked.Increment(ref addrs);
                    }
                    long done = System.Threading.Interlocked.Increment(ref pws);
                    if (local.Length > 1 << 15) { lock (writeLock) w.Write(local.ToString()); local.Clear(); }
                    if ((done & 0x3FFFFF) == 0)
                        Console.WriteLine($"... {done:N0} passwords, {System.Threading.Interlocked.Read(ref addrs):N0} addresses");
                    return local;
                },
                local => { if (local.Length > 0) lock (writeLock) w.Write(local.ToString()); });
            n = addrs;
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
            if (doF3) families.Add(RawKeyFamilies.Counters());
            if (doF4) families.Add(RawKeyFamilies.SparseDense());
            if (doF5) families.Add(RawKeyFamilies.AsciiPayloads(RawKeyFamilies.AsciiTokens));
            if (doF6) families.Add(RawKeyFamilies.StructuredDecimal());
            if (doF8) families.Add(RawKeyFamilies.NumsConstants());

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

    /// <summary>
    /// The forms of a phrase a human might actually type as a brainwallet secret. Brainwallets
    /// are exact-string, so a phrase corpus's yield is dominated by matching the *form*. We
    /// cross two bases (original, and punctuation-stripped) with four casings (as-is / lower /
    /// upper / Title), four word separators (space / none / underscore / hyphen), and the
    /// with/without-trailing-period toggle; plus the first-letter **acronym** (the classic
    /// "make a password from the initials of a sentence" trick), lower and upper. All deduped.
    /// </summary>
    public static IEnumerable<string> Variants(string phrase)
    {
        string p = phrase.Trim();
        if (p.Length == 0) yield break;
        var seen = new HashSet<string>(StringComparer.Ordinal);

        string dep = System.Text.RegularExpressions.Regex.Replace(p, "[^A-Za-z0-9 ]", "");
        dep = System.Text.RegularExpressions.Regex.Replace(dep, " +", " ").Trim();
        var bases = new List<string> { p };
        if (dep.Length > 0 && dep != p) bases.Add(dep);

        foreach (var b in bases)
        {
            var words = b.Split(' ', StringSplitOptions.RemoveEmptyEntries);
            string title = string.Join(" ", words.Select(w =>
                char.ToUpperInvariant(w[0]) + w.Substring(1).ToLowerInvariant()));

            foreach (var cased in new[] { b, b.ToLowerInvariant(), b.ToUpperInvariant(), title })
                foreach (var sep in new[] { cased, cased.Replace(" ", ""), cased.Replace(" ", "_"), cased.Replace(" ", "-") })
                {
                    string noDot = sep.TrimEnd('.');
                    foreach (var v in new[] { sep, noDot, noDot + "." })
                        if (v.Length > 0 && seen.Add(v)) yield return v;
                }

            if (words.Length >= 2)   // first-letter acronym / initialism
            {
                string ac = new string(words.Select(w => w[0]).ToArray());
                foreach (var v in new[] { ac.ToLowerInvariant(), ac.ToUpperInvariant() })
                    if (seen.Add(v)) yield return v;
            }
        }
    }
}
