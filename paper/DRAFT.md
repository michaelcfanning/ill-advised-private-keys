# BIP-39 and the Bad Seeds — Into My Arms

*Measuring Sweeping and Loss in the Weak-Mnemonic Key Space.* Working draft.

Content-first draft in markdown; port to the USENIX LaTeX template (13 pp body,
anonymized) before submission. Structure follows [../PAPER.md](../PAPER.md);
methodology is [../ECONOMICS.md](../ECONOMICS.md); charter [../MISSION.md](../MISSION.md).
Citations use Pandoc `[@key]` markers against [references.bib](references.bib);
build with `pandoc --citeproc` (see the References section).

Status tags: **[HAVE]** verified this project · **[REPRO]** measured before, must
reproduce with exact figures · **[DATA]** blocked on the ever-funded set ·
**[TODO]** unwritten. Never let a [REPRO]/[DATA] number reach the abstract until
it is [HAVE].

---

## Abstract [HAVE]

> Frame written; bracketed quantities stay `[DATA]` until measured, and no number
> enters this paragraph before it is `[HAVE]` in the ledger.

A BIP-39 seed phrase is only as strong as the entropy behind it, and a checksum
that validates *form* offers no protection against a human who chooses something
*memorable*: a repeated word, a counted sequence, a hashed password used as
entropy. Attackers have swept such keys for over a decade. Prior studies measured
`SHA256(password)` brainwallets and weak-RNG seed generation; we apply the same
read-only, funded-then-swept methodology to the BIP-39 mnemonic-pattern population
and to the family in which `SHA256(password)` is used as BIP-39 entropy. We
enumerate the memorable-mnemonic space, derive addresses across the standard HD
schemes (BIP-44/49/84/86, compressed and uncompressed), and measure — entirely
read-only, with a tool that provably cannot move funds — how much value these keys
ever held, how fast it was swept, how concentrated the drainers are, and, the
question that governs disclosure, whether fresh victims still arrive. We find the
current-balance space swept clean (0 hits across 150M+ derived addresses) and, from a
full ever-funded node walk, a **small and highly concentrated** historical loss:
across the mnemonic-pattern and raw-key (target-K) populations, 195 funded weak
addresses whose good-faith-victim loss is **at most about \$16,000 in the money of the
day** — most funded *value* is self-custody or larks, not theft — while a replication
of the `SHA256(password)` brainwallet family recovers **about 1,250 meaningfully-funded
passwords** (the same order as the decade-old study it reproduces), with good-faith-victim
loss of roughly **\$31,000 in the money of the day** (an upper bound). Sweeps are a race — median latency 0 days, a handful of bots
doing the harvesting — and **no weak address we measured holds a live balance today**.
Fresh fundings still arrive a decade on, which shapes disclosure. We report value in
the money of the day, not today's price, and separate four actors — researchers,
general users, larkers, bad guys — so self-custody and larks stay out of "loss." We
ship the defense before the specifics: a non-blocking BIP-39 strength check and a
poisoned-address denylist, contributed upstream. All code, the positive-control
self-test that makes our null results trustworthy, and a fully auditable record of the
authoring process accompany the paper.

## 1. Introduction [HAVE — prose drafted]

A public blockchain is a hostile environment for a weak secret. Every spendable
key sits in the open, and automated adversaries continuously enumerate the keys a
human might plausibly have chosen, sweeping any that hold value the instant they
are funded — the "dark forest" that observers of these chains have described for
years [@robinson2020darkforest]. For keys derived from memorable secrets this is
not a hypothetical: brainwallets built as `SHA256(password)` were farmed to
exhaustion over 2011–2015, with essentially every funded wallet emptied, often
within minutes [@vasek2016braindrain].

The seed phrase was supposed to move users away from this failure mode. BIP-39
turned a wallet's root secret into a sequence of English words with a built-in
checksum [@bip39], and BIP-32/44/49/84/86 turned that seed into an entire tree of
addresses [@bip32; @bip44; @bip49; @bip84; @bip86]. But the checksum validates
*form*, not *randomness*: a phrase that a person deliberately made easy to
remember — the same word twelve times, the wordlist in order, a favorite password
hashed into the entropy field — passes the checksum and produces perfectly valid
addresses. The security of the whole scheme rests on an assumption the standard
cannot enforce, that the entropy was actually random. Where a human substitutes a
*procedure they can memorize* for that entropy, the real strength is not the
nominal 128 or 256 bits but the length of the shortest description of that
procedure — a dozen bits, well inside an attacker's reach.

**Neighboring populations.** Prior work has characterized keys weak for adjacent
reasons. Vasek et al. measured `SHA256(password)` brainwallets, where the password
hash is used directly as the private key [@vasek2016braindrain]; Milk Sad measured
weak-RNG generation, where a broken 32-bit seed feeds an otherwise-correct BIP-39
pipeline [@milksad2023]. This paper looks at a third case: BIP-39 seed phrases that
are memorable by construction, and the family in which `SHA256(password)` is used
not as a raw key but as BIP-39 entropy (family F7). It is a different era,
derivation path, and user than the brainwallet cohort, but the downstream dynamics
of funding, sweeping, and loss are the same — so the same measurement methods
apply, and the results connect directly to that prior work.

**What we do.** We enumerate the memorable-mnemonic space by family (§4), derive
addresses across the standard HD schemes and script types (§5), and check
read-only whether each address ever held value. Detection is a local membership
test against a set of funded addresses held as 64-bit hashes, so the enumeration
runs at scale with no network in the loop (§5). From the addresses that were ever
funded we measure the economics of this population (§6–7): how prevalent funded
weak keys are per family, how fast they are swept, how concentrated the drainers
are, and whether victims are *still arriving* today — the freshness result that
decides what we can safely disclose and when. Crucially, the tool cannot move
funds: no code path constructs, signs, or broadcasts a transaction, and it is open
source so the guarantee is auditable rather than asserted (§9).

**Defense before disclosure.** The fixes that matter — a strength check in BIP-39
libraries, an import-time warning in wallets, and a poisoned-address denylist —
need to know only *which patterns* are weak, not *which addresses* are funded. We
therefore build and contribute the defense on aggregate statistics first, and
treat the question of publishing any per-address specifics as a separate, data-
driven decision governed by the disclosure tiers in §9.

### Contributions
1. A read-only, funded-then-swept measurement of the BIP-39 mnemonic-pattern
   population and the `SHA256(pw)`-as-BIP-39-entropy family (F7), applying the
   brainwallet-measurement methodology to keys those studies did not cover.
2. A freshness result: arrival rate of fresh weak-key victims over time. **[DATA]**
3. Attacker economics: on-chain sweep-fee measurement → a profitability threshold,
   reframed as a defensive design parameter. **[DATA]**
4. A shipped defense: a non-blocking BIP-39 strength check + a poisoned-address
   denylist, contributed upstream.
5. An auditable, funds-neutral methodology: an open-source scanner that provably
   cannot move funds, with a pre-registered economic analysis.

## 2. Background [HAVE — prose drafted]

**BIP-39 seeds.** A BIP-39 mnemonic encodes an initial entropy of 128–256 bits as
12–24 words drawn from a fixed 2048-word list; the last word packs a short checksum
(entropy length ÷ 32 bits) so that random typos are caught [@bip39]. The mnemonic,
optionally salted with a user passphrase, is stretched through PBKDF2 into a 512-bit
seed. Two facts matter for this work. First, the checksum is a function of the
entropy alone: **any** entropy value yields a valid mnemonic, so validity certifies
form, never randomness. Second, the map from words to entropy is public and
invertible, so an attacker who can guess the *procedure* that generated the entropy
can reproduce the seed exactly.

**HD derivation.** From the seed, BIP-32 derives a hierarchical-deterministic tree
of keys, and BIP-44/49/84/86 fix the account paths for the four address types in
use: legacy P2PKH (`m/44'`), wrapped SegWit P2SH-P2WPKH (`m/49'`), native SegWit
P2WPKH (`m/84'`), and Taproot P2TR (`m/86'`) [@bip32; @bip44; @bip49; @bip84; @bip86].
A single seed therefore fans out to many addresses; a scanner that wants to find
*any* value a weak seed touched must derive across all four schemes, and for legacy
keys across both compressed and uncompressed public-key encodings. §5 details this
fan-out.

**Brainwallets and the raw-key vs. entropy split.** A brainwallet skips BIP-39
entirely: it takes `SHA256(passphrase)` and uses the 256-bit digest *directly* as
the private key [@vasek2016braindrain; @castellucci2015brainflayer]. Our population
differs in where the memorable secret enters the pipeline. We distinguish two
targets. **Target K** (raw key): the private key itself is structured or memorable
— a small integer, a repeated byte pattern, an ASCII payload, `SHA256(password)`
used as the key (the classic brainwallet). **Target E** (entropy): the *BIP-39
entropy field* is structured or memorable — the same word repeated, the wordlist
walked in order, or `SHA256(password)` used as entropy (family F7) — after which
the standard, randomness-assuming derivation runs on top. The two targets produce
different address sets from the same human secret, so a scanner built for one does
not enumerate the other. §4 enumerates the families under each target; the full
taxonomy is in [../PATTERNS.md](../PATTERNS.md).

**Sweeping.** Once a weak address is funded, taking it is a race. Multiple
independent "drainer" bots watch for deposits to known-weak addresses and compete
to sweep them; because Bitcoin's mempool defaults to full replace-by-fee, the
sweep collapses to a fee-bidding contest whose winner takes the funds
[@vasek2016braindrain]. This mechanic is why sweep latency is typically seconds to
minutes, why a would-be rescuer cannot win the race, and why "send a warning
transaction" is not a defense but a donation to the fastest drainer (§9). It also
grounds the attacker economics of §6: the sweep is not free, so there is a deposit
size below which it is unprofitable to chase — a threshold we measure and then
repurpose as a defensive design parameter.

## 3. Threat model [HAVE, from MISSION.md]

Attack cost scales with the size of the weak-key space, not the victim count: one
pass over the enumerated space covers every user who made the mistake. "Weak = in
somebody's dictionary." The user memorizes a *procedure*, not a phrase, so the real
entropy is the length of the shortest description the author would use — a dozen
bits, regardless of the nominal 128/256.

## 4. Weak-key families [HAVE, condense PATTERNS.md]

Periodic fills (F1), hex-word fills (F2), counters/sequences (F3), sparse/dense
keys (F4), ASCII payloads (F5), **hashed-password entropy (F7)**, nothing-up-my-
sleeve constants (F8). Cost model → policy: go wide on target K, deep on target E.
Table T2.

## 5. Measurement infrastructure [HAVE]

Because a hashed key is indistinguishable from random (§8), the chain cannot be audited for
"weak-looking" keys directly — a brainwallet key looks perfect. The only available method is
to **enumerate the plausible low-entropy inputs, derive forward across the address fan-out,
and intersect with chain activity.** The opacity is not incidental; it is why enumerate-and-
intersect is the sole approach, and it is what the scanner implements.

The scanner (`src/WeakKeyScanner`, .NET 10 / NBitcoin):
- Derivation fan-out: BIP-44/49/84/86, compressed + uncompressed, P2PKH/P2SH-P2WPKH/
  P2WPKH/P2TR; brainwallet SHA256(pw) P2PKH.
- Offline oracle: an in-memory set of funded addresses as 64-bit FNV-1a hashes
  (~1 GB for 59M). Detection is a local membership test — no network, no rate limit.
- Enrichment via a read-only Esplora client: funders, sweepers, first/last seen.
- **Funds-neutral guarantee:** no code path constructs, signs, or broadcasts a
  transaction; open source makes it auditable (MISSION.md principle 2).
- **Trustworthy nulls:** the `selftest` positive control proves the offline oracle
  can emit a true positive, so an all-null scan means *swept clean*, not *blind*.
  Passed against the 59.4M-address set across P2PKH/P2SH/P2TR/segwit: (1) privkey=1
  derives to its documented canonical addresses; (2) per-type, NBitcoin's canonical
  string form is byte-identical to the set's and hits the matcher; (3) a known
  address hits, a garbage string misses.

## 6. Economic analysis [HAVE method / DATA numbers]

Per ECONOMICS.md, pre-registered: the compromise-event unit; the
victim-vs-deliberate-deposit taxonomy (published / dust ≤ 0.0001 BTC / tip ≥ 10
deposits, mean ≤ 0.002 BTC, ≥ 5 funders / victim / ambiguous); losses valued at
deposit- and sweep-time; the attacker profitability threshold; unique-victim
bounds; sweeper population; the arrival-rate series. The classifier is implemented
(`analyze --events`); loss is reported three ways (victims / +ambiguous / all).

**The sweep race taxes theft to miners.** Sweeping is effectively an all-pay fee auction:
drainers race to spend a funded weak UTXO under replace-by-fee, so the winner bids much of
the bait away in fees. Measuring each single-input sweep's fee against its bait (n=117 on the
mnemonic+target-K funded set), the median transfer to miners is **16.5%**, concentrated where
the genuine drains are: for baits below 0.01 BTC (n=103) the median is **28%** and the
value-weighted share **16%** (tail to ~90%), while the few baits ≥ 0.01 BTC (n=14) lose only
~1.3% — and those large, lightly-taxed sweeps are self-custody moves, not theft (§7.6). So the
fee erosion falls hardest on the attacker's real take. Combined with a near-zero *marginal*
cost of watching an already-enumerated space and a small, declining victim inflow (§7.3), this
is an attacker economy that is **rational only because watching is nearly free** — any positive
residual justifies continuing — while competition bids most of the small-drain value to miners;
for new entrants the residual is plausibly negative, the early dominant bots having taken the
large early hauls. It also sharpens the profitability threshold: below it a rational bot ignores
the deposit, and *above* it the fee race still transfers a large share, so attacker-retained
value is below the swept total on both sides.

**Separating seeded activity from organic use.** A single actor can manufacture what
looks like widespread adoption. In August 2013, one campaign funded 17,108 known-weak
brainwallet addresses with an identical 5,460 sats each — ten times the dust limit — from
34 batched funder addresses, and never reclaimed them: a seeding experiment, not 17,108
users. Counting funded *addresses* would fold that single campaign into user prevalence,
so we report prevalence by distinct weak key and by value distribution, and hold
seeded/honeypot-style clusters — many addresses funded in a narrow window with identical
amounts from a small funder set, left unreclaimed — separate from organic funding. This is
routine data hygiene, not a novel step, but it is load-bearing here: with the separation
our organic brainwallet count is of the same order as the earlier brainwallet study's,
while the raw funded-address total is roughly an order of magnitude larger. Where a count
omits the separation, an address-based total and a key-based total are not comparable, and
we prefer the latter throughout.

## 7. Results

> **Complete node-walk result — full chain, 2009→2026.** Scope: the mnemonic-pattern
> union (repeat/forward/backward/stride, 12+24 word), address **index 0**, over block
> heights 0–962,052 (genesis through 2026-08-08). Source: our parallel `nodewalk`
> ever-funded + sweep index over a local fully-synced bitcoind — not a third-party
> dump — validated against Blockchair on the first-ever Bitcoin transaction (§5). One
> block (962031) was skipped on an NBitcoin parse limit; impact negligible. BTC with
> USD-at-time from a daily price series (`data/btc_usd_daily.csv`, blockchain.info).
> Index 0 only, so every figure is a **lower bound**.
>
> - **Ever-funded (§7.2):** **158** distinct funded weak addresses — **127 `repeat`,
>   31 `forward`; `backward`/`stride`: 0.** Essentially all swept (1 residual UTXO).
>   Aggregate value drained: **2.588 BTC** (≈ $56.4k at deposit-time prices).
> - **Target K (§7.6) — raw-key extension:** enumerating the periodic-fill (F1) and
>   hex-word/single-byte-fill (F2) families as private keys *directly* adds **37**
>   funded addresses (20 periodic, 16 single-byte, 1 hex-word), disjoint from the
>   mnemonic set — total **195**. Separating attacker drains from self-custody by
>   *sweeper reach* (§7.6): only **79 of 195** funded addresses (~**1.81 BTC**) are
>   drainer-swept; the other 106 (~31 BTC) pay single-use destinations. The target-K
>   value is ~98% one `0xFACED`-repeat address whose flows are **self-directed, not a
>   drain** (re-funded four times, change cycling back, single-use destinations). The
>   contribution is the newly-measured key classes — not the BTC total, most of which
>   is self-custody.
> - **Loss / classification (§7.4), four-actor split + USD-at-time:** applying the
>   raced-vs-custody and researcher/user/larker/bad-guy classification (§7.6) to the 195
>   funded addresses, **good-faith-victim loss is ≤ 1.03 BTC ≈ \$16k in the money of the
>   day** (68 addresses, 61 keys) — an **upper** bound, since every key is
>   dictionary-guessable and much drained value sits on trivially-weak strings that are as
>   plausibly larks. Larker/deliberate (published test vectors): 0.78 BTC. Most funded
>   *value* is **self-custody**, not loss — e.g. the `0xFACED` key's 29.79 BTC (≈ \$190k
>   when it moved in 2018), owner-controlled. Value is extreme-concentrated (top-5 keys =
>   96.6%, Gini 0.99). We report money-of-the-day; at today's price the same coins read
>   ~100× larger and would misstate the harm.
> - **Sweep latency (§7.2) — the headline:** median **0 days in every year 2015–2026**
>   (n=386 sweeps, p90 = 1 day, max 279, mean 4.2). Funded weak addresses are drained
>   the **same day**, essentially without exception.
> - **Drainers (§7.5):** **230** distinct sweeper addresses, concentrated — the top
>   bot drained **17** distinct weak addresses, the next 14, then 7/6/6. Automated
>   harvesting, not incidental collection.
> - **Freshness (§7.3) — the verdict:** victim/ambiguous first-fundings are
>   **sustained, not declining**: 13 (2021), 13 (2022), 15 (2023), 15 (2024), 14
>   (2025), 10 (2026 partial). Fresh victims keep arriving a decade on.
> - **Read:** the *magnitude* is modest (~\$48k victim loss over a decade at index 0),
>   but the *dynamics* are an unambiguous dark-forest signature — instant,
>   concentrated, ongoing. That characterization is the contribution, independent of
>   the dollar total.
> - **Limitations:** index 0 only (lower bound); funders not captured (reported
>   funders = 0 by construction); repeated-single-word victim/deliberate classification
>   uncertain; USD is deposit-time (sweep-time basis pending); one block skipped.

### 7.1 The current-balance space is swept clean [HAVE]
- Repeated-word LIVE scan, Loyce 59M current-balance set, full 12+24-word space,
  indices 0–19 (22.3M addresses): **0 live hits**.
- CrackStation brainwallet scan: **63,941,068 passwords → 127,882,136 addresses,
  0 hits**, 1,299.5 s (~49.5k pw/s, 36 threads). Self-test confirms this is *clean*,
  not *blind*.
- Interpretation: the standing weak-key balance is ~0 — consistent with a space
  swept continuously. Present balance is the wrong signal; the loss is historical
  and must be read from ever-funded state, not current balance.

### 7.2 Ever-funded prevalence and sweep latency [HAVE]
Complete-chain node walk (index 0): **158 funded** weak addresses on the mnemonic side
(127 `repeat`, 31 `forward`; `backward`/`stride` 0), plus 37 raw target-K addresses
(§7.6) for **195 total**. Sweep-latency distribution: **median 0 days in every year
2015–2026** (n=386, p90 = 1 day, max 279) — where a sweep happens it is same-day
(Fig F1). The aggregate value that moved is 2.588 BTC on the mnemonic side, but that is
*not* loss: applying the four-actor disposition (§7.6) — raced-vs-custody by sweep
latency, plus sweeper reach — **most funded value is self-custody**, owner-moved rather
than raced (dominated by one 0xFACED raw key holding ~30 BTC), and **good-faith-victim
loss is at most 1.03 BTC ≈ \$16k in the money of the day** (68 addresses, 61 keys; an
upper bound, since every key is dictionary-guessable). No address holds a live balance
today (two residual dust UTXOs of 546 and 1,000 sats). All four columns per class —
addresses, distinct keys, raw BTC, and value-at-time — are in Table T-actor.

### 7.3 Freshness — do victims still arrive? [HAVE]
Yes. Victim/ambiguous first-fundings are **sustained, not declining**: 13 (2021),
13 (2022), 15 (2023), 15 (2024), 14 (2025), 10 (2026 partial) (Fig F2). Fresh
victims keep arriving a decade after the pattern was public — the space is active,
which is the verdict that shapes the disclosure posture (§9).

### 7.4 Confirmed hits (recon) [REPRO]
Prior API recon surfaced real funded-then-swept weak keys (e.g. repeated-word
`act`×11 + `abandon`; a 24-word `abandon…art` variant reported ≈0.35 BTC; the
brainwallet control `correct horse battery staple` ≈21.9 BTC gross, classified
deliberate-published). **Reproduce every figure exactly before use; treat famous
keys as deliberate, not victim.**

### 7.5 The drainers [HAVE]
**230** distinct sweeper addresses drained the 158 funded weak addresses, and the
set is **concentrated**: the top drainer swept **17** distinct weak addresses, the
next 14, then 7/6/6 (Fig F3). Combined with the same-day latency (§7.2), this is
automated harvesting by a handful of bots, not incidental collection.

### 7.6 Raw target-K families (F1/F2) — first funded measurement [HAVE]
The §7.2 walk covered target E (BIP-39 entropy). We then enumerated the raw-key side
(target K, §2) over the same full chain: the periodic-fill family F1 (every repeating
unit ≤ 24 bits — 33.5M units) and the hex-word/single-byte-fill family F2, each imported
*directly* as a 256-bit private key (one EC multiply, no PBKDF2), deriving P2PKH (both
compressions), P2WPKH and P2TR per key. This surfaced **37 funded** target-K addresses,
disjoint from the mnemonic set:

| Sub-family | Funded addrs | BTC received |
| --- | --- | --- |
| Periodic fills (F1) | 20 | 29.843 |
| Single-byte fills (F2) | 16 | 0.498 |
| Hex-word fills (F2) | 1 | 0.047 |

**Not every spend is a theft.** Spending a weak UTXO is an attacker *sweep* only if the
destination is a drainer; otherwise it is the key's owner moving their own funds. We
separate the two by **sweeper reach**: a *drainer* destination empties **≥2 distinct**
weak addresses (the bots of §7.5 reach 17/14/7/…), whereas a single-use destination is a
self-custody candidate. Across all 195 funded weak addresses:

| | Funded addrs | BTC received |
| --- | --- | --- |
| Drainer-swept (bot, ≥2 weak addrs) | 79 | 1.81 |
| Single-use destination (self-custody candidate) | 106 | 31.16 |

The genuine drainer population is **79 addresses holding ~1.81 BTC** — small, and it is
where the same-day-sweep latency (§7.2) and concentrated-bot structure (§7.5) actually
live. (This ~1.8 BTC matches the independent economic-classifier victim total of 1.805
BTC, §7.4.) The 31 BTC of single-use value is dominated by the `0xFACED` address, which on
inspection is **self-directed movement, not a drain**: it was re-funded and re-spent four
times each over 2018-10-20…11-06 with funds cycling back after each spend (a working hub,
not a victim emptied once and abandoned), its three spend destinations drain no other weak
address, and it appears inside large multi-party transactions alongside P2SH multisig
outputs — most consistent with a deliberately chosen vanity key (`0xFACED`) used as a
temporary routing address. Target-K *value* is therefore not loss.

The defensible target-K result is the **count**: 37 funded addresses across three
previously-unmeasured raw-key sub-families, confirming these keys are used in practice —
with most associated value self-custody and the attacker-swept subset small.

**Proportion.** The genuine drainer-swept population is 79 weak addresses over 17 years
(~1.8 BTC, ~15 fresh addresses/year) — negligible against total Bitcoin activity. The
contribution is the taxonomy of newly-observed key classes and the drain dynamics on the
small subset that is actually attacked, not any magnitude of loss, which is small.

### 7.7 Brainwallet replication on ever-funded data [HAVE]
The `SHA256(password)`-as-key family is the closest prior art (§10), so we reproduce it on
full ever-funded chain data rather than the current-balance set of §7.1. Deriving from
CrackStation (63.9M passwords → 127.9M addresses) and walking the whole chain gives **18,592
funded addresses, 0 live**. The raw count is inflated by one 2013 seeding campaign (§6); the
*organic* population — passwords funded ≥ 10k sats — is **≈1,249**, the same order as the 884
the original study reported, so this is a replication and modest extension, not a new headline.
Value is extreme-concentrated (top-5 passwords = 64%, Gini 0.99) on trivially-guessable strings
(`asdfghjkloiuytrewq`, `deadsheep`, keyboard walks, a pangram) that are as plausibly larks as
good-faith wallets. Valued in the money of the day (deposit ≈ sweep for same-day drains), the
four-actor split is:

| Actor | Addrs | Keys | BTC raw | USD @ time |
| --- | ---: | ---: | ---: | ---: |
| Good-faith victim (drained) | 610 | 596 | 76.69 | \$30,796 |
| Larker / deliberate (famous string) | 282 | 261 | 22.30 | \$12,508 |
| Single-spend (unattributed) | 104 | 103 | 12.23 | \$15,311 |
| General user (custody, self-moved) | 519 | 519 | 18.15 | \$4,535 |
| Researcher / honeypot (2013 seed) | 17,077 | 17,077 | 0.93 | \$116 |

So the raw 96 BTC of "drains" is worth **≈\$31k of good-faith-victim loss at the time** (upper
bound), the value sitting on a handful of guessable strings; a concentrated set of bots does the
harvesting (top drainer reached thousands of addresses). The larger-looking BTC totals are
self-custody, larks, or one 2013 experiment — not theft.

## 8. The defense [HAVE design]

Two gates that look plausible are both wrong. **Form-validity** is the first: a BIP-39
checksum certifies structure, not randomness, so it passes every weak seed. **Output-key
inspection** is the second and less obvious: a key derived by hashing — a brainwallet, an
AI-composed phrase, or any mnemonic after PBKDF2 — is computationally indistinguishable from
a CSPRNG key, byte-for-byte identically distributed, so no entropy, compression, or
statistical test on the key can flag it. (Measured: byte-entropy cleanly separates periodic
fills and small integers from random, but `SHA256(password)` and a secure key both sit at the
32-sample maximum.) Output inspection catches only the *raw-structural* families; the hashed
families, where most victims are, are opaque at the key level.

The deeper point is that the weakness is not an artifact to detect but an **affordance to
remove**. Deriving a key from a typed input has exactly one advantage — memorability — and
memorability is the property that makes it guessable. Nobody with a high-entropy input hashes
it to a key; they use the bytes directly. So the passphrase-to-key path does not merely permit
weak keys, it *selects* for them: its only users are the ones it endangers. The fix is to
retire the affordance, not to inspect its outputs.

What remains, ranked by cost over benefit:
1. **Remove the affordance.** Tooling should not offer "type a secret → get a key" derivation;
   where it exists, deprecate and warn. Cheapest, at the source, prevents new victims.
2. **Poisoned-address denylist.** For the families you cannot inspect, refuse funding to
   known-weak *addresses* (seeded in `Econ.SeedDenylist`). The only mechanism covering the
   opaque hashed keys, and the strongest honest claim — "not known-weak," never "proven strong."
3. **Input-side strength check.** At mnemonic generation/import, measure the entropy of the
   *entropy field* (repeated/sequential words) and flag dictionary passwords. Non-blocking.
   Same regex-then-checksum core as the VC-exposure detector that flags checksum-valid
   mnemonics committed to code (GitHub secret scanning).
4. **Output structural check.** An entropy/compressibility test on raw imported keys for the
   F1–F6/F8 families. Trivial and portable, but the lowest-value layer: it misses the hashed
   families and its target users are rare.

**On the value of all this — plainly.** These defenses are sound but their practical payoff is
small, and we say so. Every layer protects a *self-selected* population: whoever chose a
memorable key is, by definition, the one doing the dangerous thing. Universal adoption is a
large ask of the ecosystem; the beneficiaries are a handful of (sometimes well-resourced) users
saved from a self-inflicted mistake. It is closer to "do not sell hammers labelled *hit your
own head*" — retire the affordance, cheap and targeted — than to "everyone wear a helmet,"
which is what universal output-checking would be: costly and low-yield. The economics (§6)
bound the upside further: attacker net profit is already marginal — a measurable share of
every swept coin is bid away to miners in the sweep race (§6), and fresh-victim inflow is
small and declining — so the harm a perfect defense would prevent is itself modest. The contribution is
the honest mapping of which cheap intervention covers which class, and the denylist artifact,
not a claim that the sky is falling.

## 9. Ethics and disclosure [HAVE]

This work measures live theft of real funds from real people, so we treat ethics
as a design constraint, not an afterthought. Our full charter is in MISSION.md;
the operative commitments for review:

**We touch no funds, and the tool cannot.** No transaction is ever signed with a
key we did not generate — no spends, sweeps, dust, on-chain warnings, or
recovery-into-escrow. This is enforced architecturally: no code path in the
scanner constructs, signs, or broadcasts a transaction. It derives keys and
addresses and queries balances, and it is open source so the guarantee is
auditable rather than asserted. We explicitly considered and rejected a "send a
tiny amount to warn the owner" intervention: it is theft of the commingled funds
of colliding claimants (§3), and the full-RBF mempool race among drainers means a
warning transaction is simply out-bid and triggers the very sweep it warns of.

**Public data only, minimal retention.** We read public chain state and public
dumps, access no private system, and respect the rate limits and terms of any API
we query. Findings retain only `(pattern_id, derivation_path, address,
ever_funded, first_seen, swept_after)`. We store no mnemonics, seeds, or private
keys — the pattern id regenerates a candidate, so storing the secret would only
create a target.

**No attribution, no deanonymization.** We can say an address is derivable from a
low-entropy mnemonic and name the pattern class; we cannot and do not say whose it
is. Key collisions (§3) can make an ownership claim outright false, and a wrong
attribution is the one way a read-only project causes harm. We make no attempt to
cluster funders to real-world identities, and report victim counts only as bounds.

**Human subjects.** The study observes public transaction records with no
interaction with, or intervention on, any person, and collects no PII beyond
public addresses; under the Common Rule it is not human-subjects research. Where a
co-author's institution requires it, we will obtain a determination letter.
Anonymized artifact for double-blind review; open-sourced on publication.

**Defense before disclosure.** The library and wallet fixes and the
poisoned-address list need no knowledge of *which* addresses are funded, only of
*which patterns* are weak. We land those on aggregate statistics first and decide
separately what specifics to publish. The disclosure posture is deliberately
data-driven: whether funded addresses are swept in seconds or sit untouched for
years determines who publication endangers, which is precisely what §7.3–7.4
measure. Tiers, descending by impact: (1) library/wallet fixes with no offensive
detail; (2) a k-anonymity address-lookup that reveals nothing about who checks and
enumerates no target list; (3) routing specific findings through parties who can
identify owners (exchange compliance, wallet vendors, CERTs); (4) publication of
the vulnerability class with aggregate statistics and no keys. Untouched funds are
what publication could endanger; for those we publish the class without the recipe
and route through intermediaries with delay.

**Dual-use.** Enumerating weak keys is the same act whether attacker or defender;
attackers have demonstrably done it for a decade (§7.4). We reduce marginal harm
by publishing no wordlists, seeds, or a runnable target list, by shipping the
defensive detector and denylist as the primary artifact, and by withholding the
per-finding "recipe" behind the disclosure tiers above.

## 10. Related work [HAVE]

Our approach descends from a decade of weak-key measurement and reuses its methods.

**Weak-key measurement.** Heninger et al. [@heninger2012psandqs] set the template
we follow — enumerate a weak subspace, then scan the real world for keys that fall
in it — for network-device keys; we apply the same shape to Bitcoin key
derivation. On-chain, Vasek et al.'s *Bitcoin Brain Drain* [@vasek2016braindrain]
is our closest methodological ancestor: they measured `SHA256(password)`
brainwallets (884 wallets, 2011–2015, all but 21 emptied, median 21 minutes, about
a dozen competing drainers), and we reuse their instruments — sweep latency,
repeated-drain-per-family inference, compressed/uncompressed fan-out — for BIP-39
mnemonic-pattern keys and `SHA256(pw)`-as-BIP-39-entropy (F7). Castellucci's
`brainflayer` and DEF CON 23 talk [@castellucci2015brainflayer] are the offensive
counterpart, and the Ethercombing / "Blockchain Bandit" scan of weak Ethereum keys
[@ise2019ethercombing] exhibits the same drainer-cluster concentration we analyze
in §7.5, on a different key population.

**Weak generation vs. weak choice.** A parallel line studies keys weak because a
machine generated them badly: Milk Sad (CVE-2023-39910) [@milksad2023;
@cve202339910] enumerated a 32-bit Mersenne-Twister seed space feeding an
otherwise-correct BIP-39 pipeline, and Randstorm [@randstorm2023] covers weak
browser-wallet PRNGs. The root cause there is a broken RNG rather than a human
choosing something memorable, but the downstream funding and sweeping dynamics are
the same, so the measurement carries over.

**Recent adjacent measurement.** Closest in time, a Milk Sad research update
[@milksad2026update16] catalogs ever-funded classic brainwallets (SHA256-as-key);
it documents prevalence but does not report the sweep-latency, drainer-
concentration, or arrival-rate dynamics we focus on, and does not cover the BIP-39
mnemonic or F7 populations. Zhou et al. [@zhou2024keyleakage] measure theft from
keys leaked on websites, on Ethereum — a different key source in the same spirit of
characterizing real losses. Two practitioner efforts bracket our families without
measuring their outcomes: Lopp [@lopp2024repeatedword] enumerates the repeated-word
mnemonic subset, and Guiar [@guiar2025entropy] analyzes byte-level entropy patterns
in 24-word phrases. That the F7 derivation is realized in usable tooling
[@brain2bip] while weak-key "zoos" such as vuke [@vuke] omit these families is part
of why we measure them.

**Economic and clustering methods.** For the funder/sweeper analysis (§7.5) we use
the common-input-ownership clustering of Meiklejohn et al. [@meiklejohn2013fistful],
with its known limits under mixing and exchange hot wallets. For loss valuation we
follow the ransomware-economics measurements of Huang et al. [@huang2018ransomware]
and Conti et al. [@conti2019ransomwarepayments], valuing flows in USD-at-
transaction-time and reporting lower bounds; Brengel and Rossow
[@brengel2018keyleakage] similarly recover Bitcoin key leakage from chain data. The
drainer race itself is a competitive-bot latency phenomenon of the kind Torres et
al. [@torres2021frontrunner] measured for Ethereum front-running.

**Wallets and users.** Systematizations of wallet security [@houy2023wallets;
@erinle2025sok; @homoliak2024sok] catalog the attack surface our defense targets,
and usable-security work on seed-phrase management [@eleshin2025seedphrases]
explains why users reach for memorable phrases in the first place — the behavior
our strength check aims to catch.

## 11. Limitations [HAVE, from ECONOMICS.md]
Deliberate-deposit contamination; lower-bound framing (only enumerated patterns);
clustering error; selection/survivorship; price model; mempool blindness.

## 12. Conclusion [HAVE]

Memorable BIP-39 seeds are weak in the one way a checksum cannot catch, and the
addresses they produce sit in the same dark forest that emptied the brainwallets
before them. We measured that this population's *current* balance is effectively
zero (0 hits across 150M+ derived addresses) — not safety but the signature of a
space swept continuously; the loss is historical and lives in ever-funded state, not
present balance. Read from that state, the loss is **real but small and highly
concentrated**: good-faith-victim loss is on the order of tens of thousands of dollars
in the money of the day across every family we measured, most funded value is
self-custody or larks rather than theft, sweeps are same-day races run by a handful of
bots, and no weak address holds a live balance today. Fresh fundings still arrive a
decade on, so this is **ongoing, not a post-mortem** — which is what licenses a careful
disclosure posture. The magnitude is modest; the contribution is the taxonomy of
memorable-key classes and the four-actor method that keeps self-custody and larks out of
"loss." Either way the defensive conclusion is the same and shippable now: form-validity is the wrong gate, so
libraries should measure entropy strength and wallets should warn on import, and
the poisoned-address denylist lets the ecosystem refuse known-bad seeds without
anyone needing to enumerate them. We ship those first; we disclose specifics only
as the freshness data licenses.

---

## Open-science + ethics appendices (USENIX-mandatory) [HAVE — anonymize on submit]
- Open Science Appendix: the scanner, the positive-control `selftest`, and the
  analysis code are open source, released as **`badseed`** (anonymized mirror for
  double-blind review; de-anonymized on publication). The self-test makes the null
  results reproducible and trustworthy.
- **Authoring provenance (reviewable).** The paper's authoring process is itself
  auditable: every commit declares whether its content was AI-generated or
  human-written, AI commits embed the verbatim driving prompt, and
  `paper/AUTHORING-LOG.md` narrates the sequence. Method and audit commands are in
  `AUTHORSHIP.md`. Offered so reviewers can inspect exactly how the manuscript was
  produced. (Cite in the anonymized submission only in a way that does not reveal
  the repository.)
- Ethics appendix (strongly encouraged): the funds-neutral architectural guarantee
  and the tiered disclosure posture (§9).

## References

Citations use Pandoc `[@key]` markers resolved against
[references.bib](references.bib). The bibliography is generated at build time
(`pandoc --citeproc`, or `--biblatex`/`--natbib` on the LaTeX port), so this
section is populated automatically and left empty in the source.

## Numbers ledger (single source of truth — fill as measured)
| Metric | Value | Status | Source |
| --- | --- | --- | --- |
| Repeated-word live hits (22.3M addr) | 0 | HAVE | out/live_indices20.log |
| CrackStation passwords scanned | 63,941,068 | HAVE | out/brain.scan.log |
| CrackStation addresses / hits | 127,882,136 / 0 | HAVE | out/brain.scan.log |
| Self-test | PASS (8/8, 4 addr types) | HAVE | selftest vs Loyce 59.4M |
| Ever-funded (full chain, idx0, union) | 158 addr (127 repeat, 31 forward; bwd/stride 0) | HAVE | node.findings.jsonl |
| Ever-funded target-K (F1/F2, full chain) | 37 addr (20 periodic, 16 byte, 1 hex-word) | HAVE | node.findings.jsonl (combined) |
| Target-K received | 30.387 BTC (29.79 in one 0xFACED-repeat addr; ~0.6 BTC in other 36) | HAVE | analyze (combined) |
| Combined funded weak addrs | 195 (158 target-E mnemonic + 37 target-K) | HAVE | node.findings.jsonl (combined) |
| Good-faith-victim loss (mnemonic+target-K, actor split) | ≤ 1.03 BTC ≈ $16k money-of-day (68 addr, 61 keys; UPPER bound) | HAVE | analyze actor view + prices |
| Self-custody value (not loss) | 30.77 BTC (dom. by 0xFACED ~$190k-at-move) | HAVE | analyze actor view |
| Loss concentration | top-5 keys = 96.6% of value, Gini 0.99 | HAVE | analyze concentration |
| Fee race → miners (single-input sweeps, n=117) | small drains (<0.01 BTC) median 28% / value-wtd 16% (tail ~90%); large ≥0.01 BTC ~1.3% | HAVE | mempool API |
| Deliberate/test-vector | 0.781 BTC (7 zero-entropy vectors) + 46 sub-dust | HAVE | node walk |
| Aggregate swept (all events) | 2.588 BTC ≈ $56,364 | HAVE | node.latencies.tsv |
| Distinct drainers | 230 sweepers; top drained 17 / 14 / 7 weak addrs | HAVE | node walk |
| Sweep-latency median | 0 days every year 2015–2026 (n=386, p90 1d, max 279) | HAVE | node.latencies.tsv |
| Arrival victim/amb by year | 6('17) 11('19) 13('21) 15('23) 15('24) 14('25) | HAVE | node walk |
