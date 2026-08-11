using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using NBitcoin;
using NBitcoin.RPC;

namespace WeakKeyScanner;

/// <summary>
/// nodewalk — build the ever-funded + sweep index for a weak-address set directly from
/// a local bitcoind, streaming blocks in height order. For each block:
///   * every output paying a weak address is a FUNDING (adds a weak UTXO);
///   * every input spending a known weak UTXO is a SWEEP (a drain) — so we capture the
///     sweeper side (drainers, sweep latency, remaining balance), not just prevalence.
/// Emits one aggregated <see cref="Finding"/> per funded weak address, which the existing
/// <c>analyze</c> command consumes unchanged, plus a per-sweep latency TSV.
///
/// Throughput: the UTXO/sweep state machine must run in height order, but the expensive
/// per-block work — RPC fetch, block parsing, and per-output address resolution (the
/// crypto that dominates on large modern blocks) — is order-independent. So `--threads`
/// producers fetch+decode blocks ahead into a bounded reorder buffer, and a single
/// consumer applies them in strict order doing only cheap dict/state operations. The
/// result is identical to a serial walk; only the ordering-preserving work is serial.
///
/// Resumable: state (weak UTXO map + per-address accumulators + next height) is
/// checkpointed to JSON, so a run can process the synced prefix now and extend later.
/// Read-only: never constructs or signs a transaction (MISSION.md principle 2).
/// </summary>
public static class NodeWalk
{
    private sealed class Accum
    {
        public string WeakKey { get; set; } = "";
        public string Kind { get; set; } = "";
        public string Path { get; set; } = "";
        public long Received { get; set; }
        public long Balance { get; set; }
        public int Inbound { get; set; }
        public int Outbound { get; set; }
        public string? First { get; set; }
        public string? Last { get; set; }
        public HashSet<string> Sweepers { get; set; } = new(StringComparer.Ordinal);
    }

    private sealed class Utxo
    {
        public string Addr { get; set; } = "";
        public long Sats { get; set; }
        public string FundTime { get; set; } = "";
    }

    private sealed class State
    {
        public int NextHeight { get; set; }
        public Dictionary<string, Utxo> Utxo { get; set; } = new();   // "txid:n" -> weak utxo
        public Dictionary<string, Accum> Acc { get; set; } = new();   // address -> accumulator
    }

    // A block reduced to exactly what the consumer needs, with all address crypto already
    // done in the producer thread.
    private sealed class DecodedTx
    {
        public string Txid = "";
        public bool IsCoinbase;
        public (string Hash, uint N)[] Inputs = Array.Empty<(string, uint)>();
        public (int Vout, string? Addr, long Sats)[] Outputs = Array.Empty<(int, string?, long)>();
    }

    private sealed class DecodedBlock
    {
        public int Height;
        public string Day = "";
        public DecodedTx[] Txs = Array.Empty<DecodedTx>();
    }

    public static int Run(string[] args)
    {
        string? weakset = null;
        int from = -1, to = -1, checkpointEvery = 20_000, progressEvery = 5_000;
        bool resume = false, dumpOnly = false;
        string cookie = @"E:\ill-advised\bitcoin\.cookie";
        string rpcUrl = "http://127.0.0.1:8332/";
        string label = "nodewalk";
        string outFile = Path.Combine(AppContext.BaseDirectory, "out", "node.findings.jsonl");
        string latFile = Path.Combine(AppContext.BaseDirectory, "out", "node.latencies.tsv");
        string ckptFile = Path.Combine(AppContext.BaseDirectory, "out", "node.walk.state.json");
        int threads = Environment.ProcessorCount;

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--weakset": weakset = args[++i]; break;
                case "--from": from = int.Parse(args[++i]); break;
                case "--to": to = int.Parse(args[++i]); break;
                case "--resume": resume = true; break;
                case "--dump-only": dumpOnly = true; break;
                case "--cookie": cookie = args[++i]; break;
                case "--rpc": rpcUrl = args[++i]; break;
                case "--label": label = args[++i]; break;
                case "--out": outFile = args[++i]; break;
                case "--latencies": latFile = args[++i]; break;
                case "--checkpoint": ckptFile = args[++i]; break;
                case "--checkpoint-every": checkpointEvery = int.Parse(args[++i]); break;
                case "--progress": progressEvery = int.Parse(args[++i]); break;
                case "--threads": threads = int.Parse(args[++i]); break;
                default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
            }
        }

        // --dump-only: materialize findings from an existing checkpoint without walking
        // (lets us analyze a still-running walk's accumulated state mid-run).
        if (dumpOnly)
        {
            if (!File.Exists(ckptFile)) { Console.Error.WriteLine($"--dump-only needs an existing checkpoint: {ckptFile}"); return 2; }
            var st = JsonSerializer.Deserialize<State>(File.ReadAllText(ckptFile)) ?? new State();
            WriteFindings(st, label, outFile);
            Console.WriteLine($"dumped {st.Acc.Count:N0} findings from checkpoint (through height {st.NextHeight:N0}) -> {Path.GetFullPath(outFile)}");
            return 0;
        }

        if (weakset is null || !File.Exists(weakset)) { Console.Error.WriteLine("nodewalk requires --weakset FILE"); return 2; }
        if (!File.Exists(cookie)) { Console.Error.WriteLine($"cookie not found: {cookie} (is bitcoind running with this datadir?)"); return 2; }
        if (threads < 1) threads = 1;

        var net = Network.Main;
        var rpc = new RPCClient(RPCCredentialString.Parse($"cookiefile={cookie}"), new Uri(rpcUrl), net);

        int tip;
        try { tip = (int)rpc.GetBlockCount(); }
        catch (Exception ex) { Console.Error.WriteLine($"RPC unreachable ({rpcUrl}): {ex.Message}"); return 3; }

        var weak = LoadWeak(weakset);
        Console.WriteLine("ill-advised-private-keys — node walk (ever-funded + sweep index)");
        Console.WriteLine($"weakset : {weakset}  ({weak.Count:N0} addresses)");
        Console.WriteLine($"node tip: {tip:N0}");
        Console.WriteLine($"threads : {threads} (prefetch producers)");

        var state = new State();
        if (resume && File.Exists(ckptFile))
        {
            state = JsonSerializer.Deserialize<State>(File.ReadAllText(ckptFile)) ?? new State();
            Console.WriteLine($"resumed : checkpoint at height {state.NextHeight:N0} " +
                              $"({state.Acc.Count:N0} funded addrs, {state.Utxo.Count:N0} live weak UTXOs)");
        }

        int start = from >= 0 ? from : state.NextHeight;
        int end = to >= 0 ? Math.Min(to, tip) : tip;
        if (start > end) { Console.WriteLine($"nothing to do: start {start} > end {end}"); return 0; }

        Console.WriteLine($"walk    : [{start:N0}, {end:N0}]  label={label}");
        Console.WriteLine(new string('=', 72));

        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(latFile))!);
        bool latExists = File.Exists(latFile);
        using var lat = new StreamWriter(latFile, append: resume && latExists);
        if (!(resume && latExists))
            lat.WriteLine("weak_address\tfund_time\tsweep_time\tlatency_days\tvalue_sats\tsweeper_dest");

        var sw = Stopwatch.StartNew();
        long fundings = 0, sweeps = 0;
        bool stop = false;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; Console.WriteLine("\n[cancel] draining in-flight blocks, then checkpointing…"); };

        // ---- Prefetch pipeline ----
        int windowCap = Math.Max(threads * 4, 64);
        var buffer = new ConcurrentDictionary<int, DecodedBlock>();
        var slots = new SemaphoreSlim(windowCap);
        int nextToFetch = start;
        object fetchLock = new();

        var producers = new Task[threads];
        for (int t = 0; t < threads; t++)
        {
            producers[t] = Task.Run(() =>
            {
                while (!stop)
                {
                    int h;
                    lock (fetchLock) { if (nextToFetch > end) return; h = nextToFetch++; }
                    slots.Wait();
                    if (stop) { slots.Release(); return; }
                    DecodedBlock db;
                    try { db = Decode(rpc, h, net); }
                    catch (Exception ex)
                    {
                        Console.Error.WriteLine($"[skip] block {h}: {ex.Message}");
                        db = new DecodedBlock { Height = h, Day = "", Txs = Array.Empty<DecodedTx>() };
                    }
                    buffer[h] = db;
                }
            });
        }

        // ---- Consumer: strict height order, cheap state ops only ----
        int hc = start;
        for (; hc <= end && !stop; hc++)
        {
            DecodedBlock? db = null;
            var spin = new SpinWait();
            while (!buffer.TryRemove(hc, out db)) { if (stop) break; spin.SpinOnce(); }
            if (db is null) break;
            slots.Release();

            foreach (var tx in db.Txs)
            {
                if (!tx.IsCoinbase)
                {
                    foreach (var (ph, pn) in tx.Inputs)
                    {
                        var key = $"{ph}:{pn}";
                        if (!state.Utxo.TryGetValue(key, out var u)) continue;
                        var acc = state.Acc[u.Addr];
                        acc.Outbound++;
                        acc.Balance -= u.Sats;
                        acc.Last = Max(acc.Last, db.Day);
                        string dest = "";
                        foreach (var (vout, addr, sats) in tx.Outputs)
                        {
                            if (addr is null || weak.ContainsKey(addr)) continue;
                            acc.Sweepers.Add(addr);
                            if (dest.Length == 0) dest = addr;
                        }
                        sweeps++;
                        lat.WriteLine($"{u.Addr}\t{u.FundTime}\t{db.Day}\t{LatencyDays(u.FundTime, db.Day)}\t{u.Sats}\t{dest}");
                        state.Utxo.Remove(key);
                    }
                }

                foreach (var (vout, addr, sats) in tx.Outputs)
                {
                    if (addr is null || !weak.TryGetValue(addr, out var meta)) continue;
                    if (!state.Acc.TryGetValue(addr, out var acc))
                        state.Acc[addr] = acc = new Accum { WeakKey = meta.WeakKey, Kind = meta.Kind, Path = meta.Path };
                    acc.Received += sats;
                    acc.Balance += sats;
                    acc.Inbound++;
                    acc.First = Min(acc.First, db.Day);
                    acc.Last = Max(acc.Last, db.Day);
                    state.Utxo[$"{tx.Txid}:{vout}"] = new Utxo { Addr = addr, Sats = sats, FundTime = db.Day };
                    fundings++;
                }
            }

            if ((hc - start + 1) % progressEvery == 0)
            {
                double bps = (hc - start + 1) / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                int remain = end - hc;
                Console.WriteLine($"... h={hc:N0}/{end:N0} | {fundings:N0} fundings {sweeps:N0} sweeps | {state.Acc.Count:N0} funded addrs | {bps:N0} blk/s | eta {TimeSpan.FromSeconds(remain / Math.Max(0.1, bps)):hh\\:mm\\:ss}");
            }
            if ((hc - start + 1) % checkpointEvery == 0)
            {
                state.NextHeight = hc + 1;
                lat.Flush();
                Checkpoint(state, ckptFile);
            }
        }

        // Unblock and drain producers.
        stop = true;
        slots.Release(windowCap);
        try { Task.WaitAll(producers, 10_000); } catch { }

        state.NextHeight = hc;
        lat.Flush();
        Checkpoint(state, ckptFile);
        WriteFindings(state, label, outFile);

        sw.Stop();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"done: walked [{start:N0}, {(stop ? hc - 1 : end):N0}] | {fundings:N0} fundings | {sweeps:N0} sweeps | " +
                          $"{state.Acc.Count:N0} funded weak addrs | {state.Utxo.Count:N0} still-unspent | {sw.Elapsed.TotalMinutes:N1}m " +
                          $"({(hc - start) / Math.Max(0.001, sw.Elapsed.TotalSeconds):N0} blk/s)");
        Console.WriteLine($"findings : {Path.GetFullPath(outFile)}  (feed to: analyze <file> --latencies ... --prices ...)");
        Console.WriteLine($"latencies: {Path.GetFullPath(latFile)}");
        Console.WriteLine($"checkpoint: {Path.GetFullPath(ckptFile)} (next height {state.NextHeight:N0}; --resume to continue)");
        return 0;
    }

    /// <summary>Fetch + parse + resolve all output addresses for one block (the parallel-heavy work).</summary>
    private static DecodedBlock Decode(RPCClient rpc, int height, Network net)
    {
        var block = FetchBlock(rpc, height);
        var day = block.Header.BlockTime.UtcDateTime.ToString("yyyy-MM-dd");
        var txs = new DecodedTx[block.Transactions.Count];
        for (int i = 0; i < txs.Length; i++)
        {
            var tx = block.Transactions[i];
            var outs = new (int, string?, long)[tx.Outputs.Count];
            for (int n = 0; n < outs.Length; n++)
                outs[n] = (n, Addr(tx.Outputs[n].ScriptPubKey, net), tx.Outputs[n].Value.Satoshi);

            (string, uint)[] ins;
            if (tx.IsCoinBase) ins = Array.Empty<(string, uint)>();
            else
            {
                ins = new (string, uint)[tx.Inputs.Count];
                for (int k = 0; k < ins.Length; k++)
                    ins[k] = (tx.Inputs[k].PrevOut.Hash.ToString(), tx.Inputs[k].PrevOut.N);
            }
            txs[i] = new DecodedTx { Txid = tx.GetHash().ToString(), IsCoinbase = tx.IsCoinBase, Inputs = ins, Outputs = outs };
        }
        return new DecodedBlock { Height = height, Day = day, Txs = txs };
    }

    /// <summary>RPC block fetch with backoff, to ride out transient work-queue contention under many producers.</summary>
    private static Block FetchBlock(RPCClient rpc, int height)
    {
        for (int attempt = 1; ; attempt++)
        {
            try { return rpc.GetBlock(rpc.GetBlockHash(height)); }
            catch when (attempt < 5) { Thread.Sleep(200 * attempt); }
        }
    }

    private static void WriteFindings(State state, string label, string outFile)
    {
        using var w = new FindingsWriter(outFile);
        foreach (var (addr, a) in state.Acc)
        {
            var f = new Finding(
                PatternType: label,
                WeakKey: a.WeakKey,
                Kind: a.Kind,
                Path: a.Path,
                Chain: "bitcoin",
                Address: addr,
                EverFunded: true,
                TxCount: a.Inbound + a.Outbound,
                TotalReceivedSats: a.Received,
                BalanceSats: a.Balance,
                InboundTxs: a.Inbound,
                OutboundTxs: a.Outbound,
                FirstSeen: a.First,
                LastSeen: a.Last,
                Funders: Array.Empty<string>(),        // funder side needs prevout resolution; deferred
                Sweepers: a.Sweepers.ToArray(),
                ExplorerUrl: Explorers.Address(addr));
            w.Write(f);
        }
    }

    private static void Checkpoint(State state, string path)
    {
        var full = Path.GetFullPath(path);
        Directory.CreateDirectory(Path.GetDirectoryName(full)!);
        var tmp = full + ".tmp";
        File.WriteAllText(tmp, JsonSerializer.Serialize(state));
        File.Move(tmp, full, overwrite: true);   // atomic-ish: never leave a half-written checkpoint
    }

    /// <summary>
    /// The single-recipient address for a scriptPubKey, or null (bare multisig / nonstandard /
    /// OP_RETURN). Handles P2PKH/P2SH/P2WPKH/P2WSH/P2TR via GetDestinationAddress, and — crucially
    /// for the early and brainwallet eras — pay-to-pubkey (P2PK), which has no address of its own:
    /// we resolve it to the P2PKH of the embedded pubkey (as its compression is stored), which is
    /// exactly the address form our weak set holds and the form Blockchair reports for P2PK outputs.
    /// Without this, every P2PK funding is silently missed.
    /// </summary>
    private static string? Addr(Script script, Network net)
    {
        try
        {
            var a = script.GetDestinationAddress(net);
            if (a is not null) return a.ToString();
            var pk = PayToPubkeyTemplate.Instance.ExtractScriptPubKeyParameters(script);
            if (pk is not null) return pk.GetAddress(ScriptPubKeyType.Legacy, net).ToString();
            return null;
        }
        catch { return null; }
    }

    private static Dictionary<string, (string WeakKey, string Kind, string Path)> LoadWeak(string path)
    {
        var d = new Dictionary<string, (string, string, string)>(StringComparer.Ordinal);
        foreach (var line in File.ReadLines(path))
        {
            var s = line.AsSpan().Trim();
            if (s.Length == 0 || s[0] == '#') continue;
            var cols = line.Split('\t');
            var addr = cols[0].Trim();
            if (addr.Length == 0) continue;
            d[addr] = (cols.Length > 1 ? cols[1] : "", cols.Length > 2 ? cols[2] : "", cols.Length > 3 ? cols[3] : "");
        }
        return d;
    }

    private static string Min(string? cur, string day) => cur is null || string.CompareOrdinal(day, cur) < 0 ? day : cur;
    private static string Max(string? cur, string day) => cur is null || string.CompareOrdinal(day, cur) > 0 ? day : cur;

    private static int LatencyDays(string fund, string sweep)
    {
        if (DateTime.TryParse(fund, out var a) && DateTime.TryParse(sweep, out var b))
            return (int)Math.Round((b - a).TotalDays);
        return -1;
    }
}
