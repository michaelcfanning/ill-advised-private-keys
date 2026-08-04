using System.Diagnostics;
using System.Text.Json;
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
/// Resumable: the state (weak UTXO map + per-address accumulators + next height) is
/// checkpointed to JSON, so a run can process the synced prefix now and extend later as
/// the node catches up — the "process 50% now, batch the rest" workflow. Read-only:
/// never constructs or signs a transaction (MISSION.md principle 2).
///
/// No price data: bitcoind carries no USD, so USD-at-time stays node-gated (filled from a
/// price series later). The weak set holds only weak addresses (rare), so the UTXO map
/// and accumulators are small regardless of chain size.
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

    public static int Run(string[] args)
    {
        string? weakset = null;
        int from = -1, to = -1, checkpointEvery = 20_000, progressEvery = 2_000;
        bool resume = false;
        string cookie = @"E:\ill-advised\bitcoin\.cookie";
        string rpcUrl = "http://127.0.0.1:8332/";
        string label = "nodewalk";
        string outFile = Path.Combine(AppContext.BaseDirectory, "out", "node.findings.jsonl");
        string latFile = Path.Combine(AppContext.BaseDirectory, "out", "node.latencies.tsv");
        string ckptFile = Path.Combine(AppContext.BaseDirectory, "out", "node.walk.state.json");

        for (int i = 1; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--weakset": weakset = args[++i]; break;
                case "--from": from = int.Parse(args[++i]); break;
                case "--to": to = int.Parse(args[++i]); break;
                case "--resume": resume = true; break;
                case "--cookie": cookie = args[++i]; break;
                case "--rpc": rpcUrl = args[++i]; break;
                case "--label": label = args[++i]; break;
                case "--out": outFile = args[++i]; break;
                case "--latencies": latFile = args[++i]; break;
                case "--checkpoint": ckptFile = args[++i]; break;
                case "--checkpoint-every": checkpointEvery = int.Parse(args[++i]); break;
                case "--progress": progressEvery = int.Parse(args[++i]); break;
                default: Console.Error.WriteLine($"unknown option: {args[i]}"); return 2;
            }
        }
        if (weakset is null || !File.Exists(weakset)) { Console.Error.WriteLine("nodewalk requires --weakset FILE"); return 2; }
        if (!File.Exists(cookie)) { Console.Error.WriteLine($"cookie not found: {cookie} (is bitcoind running with this datadir?)"); return 2; }

        var net = Network.Main;
        var rpc = new RPCClient(RPCCredentialString.Parse($"cookiefile={cookie}"), new Uri(rpcUrl), net);

        int tip;
        try { tip = (int)rpc.GetBlockCount(); }
        catch (Exception ex) { Console.Error.WriteLine($"RPC unreachable ({rpcUrl}): {ex.Message}"); return 3; }

        // Load the weak set: "address\tweakkey\tkind\tpath" (extra cols optional; also
        // accepts a plain address-per-line file).
        var weak = LoadWeak(weakset);
        Console.WriteLine("ill-advised-private-keys — node walk (ever-funded + sweep index)");
        Console.WriteLine($"weakset : {weakset}  ({weak.Count:N0} addresses)");
        Console.WriteLine($"node tip: {tip:N0}");

        // Resume from checkpoint if asked and present.
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
        // Append latencies (a resumed run continues the same file); header once.
        bool latExists = File.Exists(latFile);
        using var lat = new StreamWriter(latFile, append: resume && latExists);
        if (!(resume && latExists))
            lat.WriteLine("weak_address\tfund_time\tsweep_time\tlatency_days\tvalue_sats\tsweeper_dest");

        var sw = Stopwatch.StartNew();
        long fundings = 0, sweeps = 0;
        bool stop = false;
        Console.CancelKeyPress += (_, e) => { e.Cancel = true; stop = true; Console.WriteLine("\n[cancel] finishing current block, then checkpointing…"); };

        int h = start;
        for (; h <= end && !stop; h++)
        {
            Block block;
            try { block = rpc.GetBlock(rpc.GetBlockHash(h)); }
            catch (Exception ex) { Console.Error.WriteLine($"[warn] block {h}: {ex.Message}; retrying once"); System.Threading.Thread.Sleep(500); try { block = rpc.GetBlock(rpc.GetBlockHash(h)); } catch { Console.Error.WriteLine($"[skip] block {h}"); continue; } }

            string day = block.Header.BlockTime.UtcDateTime.ToString("yyyy-MM-dd");

            foreach (var tx in block.Transactions)
            {
                // Sweeps: inputs spending a known weak UTXO.
                if (!tx.IsCoinBase)
                {
                    foreach (var vin in tx.Inputs)
                    {
                        var key = $"{vin.PrevOut.Hash}:{vin.PrevOut.N}";
                        if (!state.Utxo.TryGetValue(key, out var u)) continue;
                        var acc = state.Acc[u.Addr];
                        acc.Outbound++;
                        acc.Balance -= u.Sats;
                        acc.Last = Max(acc.Last, day);
                        // Sweeper = where the drained value went (this tx's non-weak outputs).
                        string dest = "";
                        foreach (var so in tx.Outputs)
                        {
                            var a = Addr(so.ScriptPubKey, net);
                            if (a is null || weak.ContainsKey(a)) continue;
                            acc.Sweepers.Add(a);
                            if (dest.Length == 0) dest = a;
                        }
                        sweeps++;
                        lat.WriteLine($"{u.Addr}\t{u.FundTime}\t{day}\t{LatencyDays(u.FundTime, day)}\t{u.Sats}\t{dest}");
                        state.Utxo.Remove(key);
                    }
                }

                // Fundings: outputs paying a weak address.
                var txid = tx.GetHash().ToString();
                for (int n = 0; n < tx.Outputs.Count; n++)
                {
                    var a = Addr(tx.Outputs[n].ScriptPubKey, net);
                    if (a is null || !weak.TryGetValue(a, out var meta)) continue;
                    long sats = tx.Outputs[n].Value.Satoshi;
                    if (!state.Acc.TryGetValue(a, out var acc))
                        state.Acc[a] = acc = new Accum { WeakKey = meta.WeakKey, Kind = meta.Kind, Path = meta.Path };
                    acc.Received += sats;
                    acc.Balance += sats;
                    acc.Inbound++;
                    acc.First = Min(acc.First, day);
                    acc.Last = Max(acc.Last, day);
                    state.Utxo[$"{txid}:{n}"] = new Utxo { Addr = a, Sats = sats, FundTime = day };
                    fundings++;
                }
            }

            if ((h - start + 1) % progressEvery == 0)
            {
                double bps = (h - start + 1) / Math.Max(0.001, sw.Elapsed.TotalSeconds);
                int remain = end - h;
                Console.WriteLine($"... h={h:N0}/{end:N0} | {fundings:N0} fundings {sweeps:N0} sweeps | {state.Acc.Count:N0} funded addrs | {bps:N0} blk/s | eta {TimeSpan.FromSeconds(remain / Math.Max(0.1, bps)):hh\\:mm\\:ss}");
            }
            if ((h - start + 1) % checkpointEvery == 0)
            {
                state.NextHeight = h + 1;
                lat.Flush();
                Checkpoint(state, ckptFile);
            }
        }

        state.NextHeight = h; // next unprocessed height (loop exited at h = last+1, or on stop at current)
        lat.Flush();
        Checkpoint(state, ckptFile);
        WriteFindings(state, label, outFile);

        sw.Stop();
        Console.WriteLine(new string('=', 72));
        Console.WriteLine($"done: walked [{start:N0}, {(stop ? h - 1 : end):N0}] | {fundings:N0} fundings | {sweeps:N0} sweeps | " +
                          $"{state.Acc.Count:N0} funded weak addrs | {state.Utxo.Count:N0} still-unspent | {sw.Elapsed.TotalMinutes:N1}m");
        Console.WriteLine($"findings : {Path.GetFullPath(outFile)}  (feed to: analyze <file> --events out\\events.jsonl)");
        Console.WriteLine($"latencies: {Path.GetFullPath(latFile)}");
        Console.WriteLine($"checkpoint: {Path.GetFullPath(ckptFile)} (next height {state.NextHeight:N0}; --resume to continue)");
        return 0;
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
