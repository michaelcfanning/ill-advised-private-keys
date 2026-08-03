using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO.Compression;

namespace WeakKeyScanner;

/// <summary>
/// The ever-funded oracle. Streams the Blockchair Bitcoin outputs dumps
/// (blockchair_bitcoin_outputs_YYYYMMDD.tsv.gz) and keeps every output whose
/// recipient is one of our weak-key addresses. Each match is a *funding event* for
/// a weak address, carrying the deposit time and the deposit-time USD value — so
/// this single pass yields ever-funded prevalence, per-year arrival, and the loss
/// basis, none of which the current-balance set can show. The sweep side (latency,
/// drainers) is filled by API enrichment on the small resulting hit set.
///
/// Memory: the weak set is held as 64-bit FNV-1a hashes only (~8 bytes each), so
/// even the ~128M brainwallet-address space fits in ~2 GB. A matched row still
/// prints the real recipient string, taken from the dump row itself. 64-bit
/// collisions are astronomically rare and are re-verified during enrichment.
/// </summary>
public static class BlockchairScan
{
    // Outputs dump columns (11): 0 block_id, 1 transaction_hash, 2 index, 3 time,
    // 4 value, 5 value_usd, 6 recipient, 7 type, 8 script_hex, 9 is_from_coinbase,
    // 10 is_spendable.
    private const int ColTime = 3, ColValue = 4, ColValueUsd = 5, ColRecipient = 6;

    public static int Run(string[] args)
    {
        string? weakset = null, dumps = null;
        string outFile = Path.Combine(AppContext.BaseDirectory, "out", "bc.funding.tsv");
        int threads = 0;
        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--weakset": weakset = args[++i]; break;
                case "--dumps": dumps = args[++i]; break;
                case "--out": outFile = args[++i]; break;
                case "--threads": threads = int.Parse(args[++i]); break;
                default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
            }
        }
        if (weakset is null || !File.Exists(weakset)) { Console.Error.WriteLine("bcscan requires --weakset FILE"); return 2; }
        if (dumps is null || !Directory.Exists(dumps)) { Console.Error.WriteLine("bcscan requires --dumps DIR"); return 2; }
        threads = threads > 0 ? threads : Environment.ProcessorCount;

        Console.WriteLine("ill-advised-private-keys - Blockchair outputs scan (ever-funded)");
        Console.WriteLine($"weakset : {weakset}");
        Console.WriteLine($"dumps   : {dumps}");
        Console.WriteLine($"out     : {Path.GetFullPath(outFile)}");

        var sw = Stopwatch.StartNew();
        var weak = LoadWeakSet(weakset);
        Console.WriteLine($"weak addresses loaded: {weak.Count:N0} (FNV-1a hashes) in {sw.Elapsed.TotalSeconds:N1}s");

        var files = Directory.GetFiles(dumps, "blockchair_bitcoin_outputs_*.tsv.gz");
        Array.Sort(files);
        Console.WriteLine($"dump files: {files.Length:N0}");
        Console.WriteLine($"threads : {threads}");
        Console.WriteLine(new string('=', 72));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(outFile))!);
        using var outWriter = new StreamWriter(outFile, append: false);
        outWriter.WriteLine("address\ttime\tvalue_sats\tvalue_usd\tsource_file");
        var writeLock = new object();

        long rows = 0, matches = 0, filesDone = 0;
        var opts = new ParallelOptions { MaxDegreeOfParallelism = threads };
        Parallel.ForEach(files, opts, file =>
        {
            long localRows = 0;
            var buf = new List<string>();
            string tag = Path.GetFileName(file);
            try
            {
                using var fs = File.OpenRead(file);
                using var gz = new GZipStream(fs, CompressionMode.Decompress);
                using var reader = new StreamReader(gz);
                bool first = true;
                string? line;
                while ((line = reader.ReadLine()) is not null)
                {
                    if (first) { first = false; continue; } // header
                    localRows++;
                    var cols = line.Split('\t');
                    if (cols.Length <= ColRecipient) continue;
                    var recipient = cols[ColRecipient];
                    if (recipient.Length == 0) continue;
                    if (!weak.Contains(OfflineSetOracle.Fnv1a(recipient))) continue;
                    buf.Add($"{recipient}\t{cols[ColTime]}\t{cols[ColValue]}\t{cols[ColValueUsd]}\t{tag}");
                }
            }
            catch (Exception ex) { Console.Error.WriteLine($"[warn] {tag}: {ex.Message}"); }

            long d = Interlocked.Increment(ref filesDone);
            Interlocked.Add(ref rows, localRows);
            if (buf.Count > 0)
            {
                Interlocked.Add(ref matches, buf.Count);
                lock (writeLock) { foreach (var b in buf) outWriter.WriteLine(b); }
            }
            if (d % 250 == 0 || d == files.Length)
                Console.WriteLine($"... {d:N0}/{files.Length} files | {Interlocked.Read(ref rows):N0} rows | {Interlocked.Read(ref matches):N0} matches | {sw.Elapsed.TotalMinutes:N1}m");
        });

        sw.Stop();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"done: {rows:N0} output rows scanned | {matches:N0} weak-address fundings | {sw.Elapsed.TotalMinutes:N1}m");
        Console.WriteLine($"funding events: {Path.GetFullPath(outFile)}");
        return 0;
    }

    /// <summary>Load weak addresses (one per line, '#'/blank skipped) as FNV-1a hashes.</summary>
    private static HashSet<ulong> LoadWeakSet(string path)
    {
        var set = new HashSet<ulong>();
        foreach (var line in File.ReadLines(path))
        {
            var s = line.AsSpan().Trim();
            if (s.Length == 0 || s[0] == '#') continue;
            int cut = s.IndexOfAny(' ', '\t');
            var addr = (cut < 0 ? s : s[..cut]).ToString();
            set.Add(OfflineSetOracle.Fnv1a(addr));
        }
        return set;
    }
}
