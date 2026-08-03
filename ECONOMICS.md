# Economics

This document pre-registers how we turn on-chain observations into economic
claims, *before* the ever-funded index exists to produce them. The definitions
below are fixed now so that later we cannot tune a victim filter or a loss basis
until the numbers flatter the argument. Where a choice is genuinely open we say
so and commit to reporting the result under every candidate rule.

The framing is in [MISSION.md](MISSION.md); the enumerable spaces in
[PATTERNS.md](PATTERNS.md). This file is the bridge from "an address was funded
and swept" to "the defense is worth shipping."

## The one number the paper exists to estimate

The primary deliverable is a client-side check — a strength advisory in BIP-39
libraries and a warning at import — which costs approximately nothing to ship. So
its expected value is just the loss it prevents:

```
E[loss prevented] ≈ arrival_rate(fresh victims) × mean_loss(per victim) × coverage(our list)
```

Every measurement below feeds one of those three terms. If the product is a
positive, *ongoing* quantity, the defense is unarguable and disclosure is urgent;
if victimization ended years ago, this is a history paper. The economics is not
decoration — it is the load-bearing argument, and its sign is what we do not yet
know.

## Unit of analysis: the compromise event

Not the address, not the person — the **compromise event**: a weak-key address
that received value and had it taken by someone other than the depositor. One
address can host several events (re-funded after each sweep); one person can
suffer many. All totals are sums over events, with person-level and address-level
counts reported separately and always as bounds (see *Unique victims*).

An event is characterized by:

```
(pattern_id, address, funding_txs, sweep_txs,
 deposit_value_btc, deposit_value_usd_at_deposit,
 swept_value_btc,   swept_value_usd_at_sweep,
 sweep_latency, funder_cluster, sweeper_cluster, first_seen, classification)
```

`classification` is the victim/deliberate label defined next, and gates whether
the event enters the loss total.

## The central confound: victim vs. deliberate deposit

Value on a weak address is not automatically loss. Famous keys accumulate tips,
jokes, "I was here" watermarks, honeypots (researchers and attackers baiting each
other), and deliberate burns. The 21.9 BTC that has flowed through
`correct horse battery staple` is overwhelmingly *not* victim loss — it is
hundreds of tiny demonstrative sends to a key everyone knows is public. Naive
brainwallet loss headlines died on exactly this rock, and a reviewer will reach
for it first.

We therefore classify every event before counting it. The rule is
**deposit-behavior based, not amount-based alone**, and pre-registered here with
concrete thresholds (implemented in the `analyze` command as `Econ.Classify`; the
constants and this text must stay in sync, and any change is a methodology change
recorded in git):

- **Deliberate — published (excluded from loss).** The address or key matches a
  *published* weak key (famous brainwallets, `abandon…about`, `privkey=1`, test
  vectors — a maintained denylist, seeded in `Econ.SeedDenylist`).
- **Deliberate — dust (excluded).** A single deposit (`inbound ≤ 1`) at or below
  **0.0001 BTC (10,000 sat)**.
- **Deliberate — tip (excluded).** The tip signature: **≥ 10 deposits**, mean
  deposit **≤ 0.002 BTC (200,000 sat)**, from **≥ 5 distinct funders** — a public
  curiosity, not a wallet in use.
- **Plausible victim (counted).** A non-published weak address, funded above the
  dust floor, and swept (`outbound > 0`) — not left standing as a wallet.
- **Ambiguous (reported, quarantined).** Everything else (e.g. funded above dust
  but never swept, still holding a balance). We publish the totals three ways —
  victims only, victims + ambiguous, and everything — so the reader sees the
  sensitivity rather than trusting our line-drawing.

The denylist of published keys is itself a contribution: it is the seed of the
poisoned-address list the defense ships. These amount thresholds are deliberately
conservative and will be revisited against the observed profitability threshold
(below) once sweep-fee data exists; that revision, if any, is logged in git.

## Loss, valued two ways

BTC value and USD value diverge, and *when* we value matters:

- **Victim loss basis** = deposit value in USD at the *deposit* block time. This
  is what the victim actually parted with, and the honest "how much did people
  lose" figure.
- **Attacker realized gain** = swept value in USD at the *sweep* block time. What
  the drainer walked away with.

These differ by fees, by any price move during the seconds-to-years the funds
sat, and by partial sweeps. We report both; the gap between them is itself a
finding (attackers capturing appreciation, or eating depreciation). The headline
"total losses at time of transaction" is the sum of victim-loss-basis over
counted events. All figures also reported in BTC, which is assumption-free.

## Attacker economics: the interesting half

The defensive question hides here. Sweeping is not free, and its costs are
partly *on-chain and directly measurable*:

- **Sweep fees (measured).** Every sweep transaction's fee is on the chain. Summed
  per sweeper and per pattern family, this is the drainers' marginal operating
  cost.
- **The profitability threshold (derived).** Below some deposit size, fee plus
  expected fee-race loss exceeds the take, and sweeping does not pay. We estimate
  that break-even from the observed fee distribution. It is not a footnote — it is
  a **defensive design parameter**: it tells a wallet which amounts are worth
  warning about and which are beneath an attacker's notice.
- **Fee-race intensity (partial).** MISSION.md's rejected "warn with a penny" idea
  fails because a dozen full-RBF drainers bid the fee up in the mempool. Confirmed
  sweeps show the winning fee; the losing replaced bids are only visible with a
  mempool archive, so we bound this where we can and flag it where we cannot.
- **Enumeration and infrastructure (modeled, not measured).** One-time compute to
  cover the key space (see the PATTERNS.md cost model) plus always-on
  monitoring. Reported as an order-of-magnitude model, clearly labeled as such.

Attacker ROI = realized gain − fees − amortized infrastructure. The first two are
observable; the third is bounded. That a rational drainer ignores sub-threshold
dust is not a safe harbor for users — it is a quantitative boundary on where the
defense must fire.

## Unique victims: report bounds, never a point

An address is not a person, and crypto makes the mapping genuinely hard.

- **Upper bound:** distinct compromise events (funded-then-swept weak addresses).
- **Lower-ish bound:** distinct **funder** clusters under the common-input-ownership
  heuristic, acknowledging that an exchange hot-wallet funding source collapses
  many real victims into one cluster and cannot be attributed.
- We never publish a single victim count. We publish the range and the method,
  and we never attribute an event to a named person — consistent with MISSION.md
  principle 8.

## Sweepers: how many actors, and what we can tell

Cluster the **destination** side of sweeps (the `analyze` command's drainer
aggregation is the starting point). Per sweeper cluster:

- **Longevity:** first-seen to last-seen. Is this a bot that has run for years?
- **Total gains over time:** cumulative realized value, as a time series.
- **Behavioral fingerprint:** sweep-latency distribution (sub-minute ⇒ automated),
  fee strategy, RBF usage, consolidation and destination-reuse patterns.
- **Concentration:** we expect a power law — a few dominant operators and a long
  tail. "N actors; the top k captured X% of value over Y years" is the target
  sentence.

This answers a MISSION.md open question directly: how many distinct drainers work
the BIP-39 pattern space, and do they race.

## The trend question: arrival rate (the crux)

Do fresh victims still appear? First-funding date of each newly observed weak
address, bucketed by period, gives the arrival rate — the single series that
decides the disclosure posture and the sign of E[loss prevented]. If new
non-published weak addresses are still funded in 2024–2026, the defense is live.

This **cannot** come from a current-balance set (a swept address holds nothing
now); it requires the **ever-funded index**. That is exactly what the local
`bitcoind` (txindex, ~29% synced as of 2026-08-03) is being built to provide.
Until it finishes, arrival rate is the one headline number we cannot yet compute,
and we do not guess it.

## Price valuation methodology

- One block-granular BTC/USD series (candidate: CoinMetrics community or Kraken
  OHLC), spot at the block time of the relevant tx. Fixed choice, stated.
- Sensitivity check against a second source; report the delta.
- No VWAP, no exchange cherry-picking. Every USD figure carries its BTC
  counterpart so the assumption is inspectable.

## Threats to validity (state them before a reviewer does)

1. **Deliberate-deposit contamination** — the victim filter above; reported three
   ways.
2. **Lower-bound framing** — every total covers only the patterns *our* generator
   enumerates. We never say "total weak-key losses"; we say "losses over this
   defined weak-key universe," a floor.
3. **Clustering error** — heuristics have false merges/splits; reported as bounds,
   never as actor identities.
4. **Selection/survivorship** — we observe funded weak addresses; unfunded ones are
   invisible and irrelevant to loss, but we say so.
5. **Price model** — single series, sensitivity-checked.
6. **Mempool blindness** — losing fee-race bids are mostly unrecoverable
   post-hoc; fee-race claims are bounded, not exact.

## Data sources and what the node unlocks

- **Have now:** the Esplora API deep-analysis path (funders, sweepers, dates,
  amounts) — correct for the hit set already found, but rate-limited, so unfit for
  a full historical backfill.
- **The node unlocks:** the ever-funded index (arrival rates), the full spend
  graph (clustering both sides), and unmetered queries at scale. The economics
  section is genuinely gated on that sync — the critical path is the node build
  and the pattern derivation, and both are moving.

## Output: the schema `analyze` must emit

`analyze FILE --events OUT.jsonl` emits one `EconEvent` record per compromise
event with the fields listed under *Unit of analysis*, and prints the
classification breakdown, the three-way loss totals, and the by-year arrival
series. The classifier writes the `classification` label; the loss totals are a
group-by over it. Nothing about a mnemonic or key is stored — only the pattern_id,
per MISSION.md principle 4.

Implemented now, from a `Finding`: classification, funding/sweep tx counts,
deposit/swept BTC, funder/sweeper counts, first/last seen, active-window days.
Node-gated (declared as null fields until the ever-funded index + a price series
exist): `deposit_value_usd_at_deposit`, `swept_value_usd_at_sweep`, and precise
`sweep_latency_seconds` — all three need per-transaction timestamps the aggregate
enrichment does not yet retain. Per-sweeper longevity/cumulative-gain aggregates
and full arrival rates likewise firm up once the index replaces the rate-limited
API as the data source.

## Pre-registration statement

The victim/deliberate taxonomy, the two loss bases, the profitability-threshold
method, the unique-victim bounds, and the arrival-rate metric are fixed as of
this commit. If the node data forces a revision, the change and its motivation
are recorded in git history, so the difference between a principled correction and
a result-chasing one is auditable — the same standard MISSION.md sets for the
scanner's read-only guarantee.
