# BIP-39 and the Bad Seeds — Into My Arms

*Measuring sweeping and loss in the weak-mnemonic key space.* Working draft; port to the
USENIX template (13 pp., anonymized) before submission. Citations use Pandoc `[@key]`
markers against [references.bib](references.bib).

---

## Abstract

A BIP-39 seed phrase is only as strong as the entropy behind it, and a checksum that
validates *form* offers no protection against a human who chooses something *memorable*: a
repeated word, a counted sequence, a password hashed into the entropy field. Attackers have
swept such keys for over a decade. Prior work measured `SHA256(password)` brainwallets and
weak-RNG generation; we extend the same read-only, funded-then-swept method to the BIP-39
memorable-mnemonic population, the raw-key (target-K) families, and the family in which
`SHA256(password)` is used as BIP-39 *entropy* (F7) — keys those studies did not cover — over
the full chain to 2026.

We find the current-balance space swept clean (0 hits across 150M+ derived addresses) and,
from a full ever-funded node walk, a **small and highly concentrated** historical loss. Across
the mnemonic and target-K populations, 195 funded weak addresses carry a good-faith-victim
loss of **at most ≈\$16,000 in the money of the day**; most funded *value* is self-custody or
larks, not theft. A replication of the brainwallet family recovers ≈1,250 meaningfully-funded
passwords — the same order as the decade-old study it reproduces — with good-faith-victim loss
of **≈\$31,000 at the time** (an upper bound). Sweeps are a race (median latency 0 days, a
handful of bots), and **no weak address we measured holds a live balance today.**

We report value in the money of the day, and a reproducible four-actor classifier —
researchers, general users, larkers, bad guys — keeps self-custody and larks out of "loss."
The economics are self-limiting: sweeping is an all-pay fee auction that hands a large share
of each small theft to miners, so watching pays only because an already-enumerated space is
near-free to monitor. No per-use defense has a workable cost/benefit, a point we make
precisely rather than paper over with a mandate. All code, a positive-control self-test that
makes our null results trustworthy, and an auditable record of the authoring process
accompany the paper.

## 1. Introduction

A public blockchain is a hostile environment for a weak secret. Every spendable key sits in
the open, and automated adversaries continuously enumerate the keys a human might plausibly
have chosen, sweeping any that hold value the instant they are funded — the "dark forest"
observers have described for years [@robinson2020darkforest]. For keys derived from memorable
secrets this is not hypothetical: `SHA256(password)` brainwallets were farmed to exhaustion
over 2011–2015, nearly every funded wallet emptied, often within minutes [@vasek2016braindrain].

The seed phrase was meant to end that failure mode. BIP-39 turns a wallet's root secret into
words with a built-in checksum [@bip39], and BIP-32/44/49/84/86 turn the seed into a tree of
addresses [@bip32; @bip44; @bip49; @bip84; @bip86]. But the checksum validates *form*, not
*randomness*: the same word twelve times, the wordlist in order, or a favorite password hashed
into the entropy field all pass it and produce valid addresses. The scheme's security rests on
an assumption the standard cannot enforce. Where a human substitutes a *procedure they can
memorize* for random entropy, the real strength is not 128 or 256 bits but the length of the
shortest description of that procedure — a dozen bits, well inside reach.

**Neighboring populations.** Vasek et al. measured `SHA256(password)` used directly as the
private key [@vasek2016braindrain]; Milk Sad measured weak-RNG generation, a broken 32-bit
seed feeding an otherwise-correct pipeline [@milksad2023]. We look at a third case:
memorable-by-construction BIP-39 phrases, and `SHA256(password)` used as *entropy* (F7). A
different era, path, and user than the brainwallet cohort — but the downstream funding and
sweeping dynamics are the same, so the methods and results connect directly.

**What we do.** We enumerate the memorable-key space by family (§4), derive addresses across
the standard HD schemes and script types (§5), and check read-only whether each address ever
held value via a local membership test against funded addresses held as 64-bit hashes. From
the ever-funded set we measure prevalence per family, sweep latency, drainer concentration,
the attacker economics (§6), and whether victims *still arrive* today (§7). The tool cannot
move funds: no code path constructs, signs, or broadcasts a transaction, and it is open source
so the guarantee is auditable rather than asserted (§9).

**Disclosure posture.** Aggregate statistics — which patterns are weak, prevalence, latency,
drainer structure — need only *which patterns* are weak, not *which addresses* are funded, so
we report them freely; publishing any per-address specifics is a separate, data-driven
decision governed by the freshness result and the tiers of §9. On prevention we are
deliberately modest: §8 shows no per-use check has a workable cost/benefit here, so the
actionable guidance is narrow — do not offer memorable-secret-to-key derivation — not a
universal mandate.

### Contributions

1. A read-only, funded-then-swept measurement across the memorable-mnemonic (target E), raw-key
   (target K), and `SHA256(pw)`-as-entropy (F7) families — keys prior brainwallet studies did
   not cover, over the full chain to 2026.
2. A reproducible **funder-intent classifier** — researcher/honeypot, general-user custody,
   larks, and genuine victims — that turns the manual researcher/owner exclusions of prior
   brainwallet work into a coded, uniform split. It shows the honest good-faith-victim loss is
   small and highly concentrated, most funded value being self-custody or larks, not theft.
3. A novel **attacker-economics** result: sweeping is an all-pay fee auction that taxes a large
   share of each small drain to miners, so watching is rational only because an enumerated
   space is near-free to monitor — net drainer profit is marginal and declining.
4. A **freshness** result: fresh fundings of weak addresses still arrive a decade on, sustained
   rather than declining — the verdict that governs disclosure.
5. An **honestly-scoped defense**: no per-use check pencils out (a hashed key is
   indistinguishable from a good one; a denylist is ~100% cost for ~0 yield), so the one cheap
   move is retiring the memorable-secret-to-key affordance — explicitly not a universal mandate.
6. A **funds-neutral, auditable methodology**: an open-source scanner that provably cannot move
   funds, made reproducible by a positive-control self-test.

## 2. Background

**BIP-39 seeds.** A BIP-39 mnemonic encodes 128–256 bits of entropy as words from a 2048-word
list; the last word packs a checksum so random typos are caught [@bip39]. Two facts matter.
The checksum is a function of the entropy alone — *any* entropy yields a valid mnemonic, so
validity certifies form, never randomness. And the word-to-entropy map is public and
invertible, so an attacker who guesses the *procedure* that generated the entropy reproduces
the seed exactly.

**HD derivation.** BIP-32 derives a tree of keys from the seed, and BIP-44/49/84/86 fix the
account paths for the four address types: legacy P2PKH, wrapped SegWit P2SH-P2WPKH, native
SegWit P2WPKH, and Taproot P2TR [@bip32; @bip44; @bip49; @bip84; @bip86]. One seed fans out to
many addresses, so a scanner must derive across all four schemes and, for legacy keys, both
public-key compressions (§5).

**Raw key vs. entropy.** A brainwallet skips BIP-39 and uses `SHA256(passphrase)` *directly* as
the private key [@vasek2016braindrain; @castellucci2015brainflayer]. Our population differs in
*where* the memorable secret enters. **Target K** (raw key): the private key itself is
structured — a small integer, a repeated byte pattern, an ASCII payload, or `SHA256(password)`
as the key. **Target E** (entropy): the *entropy field* is structured — a repeated word, the
wordlist in order, or `SHA256(password)` as entropy (F7) — after which the standard derivation
runs on top. The two produce different addresses from the same secret, so a scanner built for
one does not enumerate the other. §4 lists the families; the full taxonomy is in
[../PATTERNS.md](../PATTERNS.md).

**Sweeping.** Once a weak address is funded, taking it is a race: independent "drainer" bots
watch known-weak addresses and compete to sweep, and because the mempool defaults to full
replace-by-fee, the sweep is a fee-bidding contest whose winner takes the funds
[@vasek2016braindrain]. This is why latency is seconds to minutes, why a rescuer cannot win the
race, and why a "warning transaction" is a donation to the fastest drainer (§9). It also
grounds the economics of §6: the sweep costs a fee, so below some deposit size chasing it is
unprofitable.

## 3. Threat model

Attack cost scales with the size of the weak-key space, not the victim count: one pass covers
every user who made the same mistake. "Weak" means "in somebody's dictionary." The user
memorizes a *procedure*, not a phrase, so the real entropy is the length of its shortest
description — a dozen bits, regardless of the nominal 128 or 256.

## 4. Weak-key families

We enumerate each family under both targets; the same pattern yields unrelated addresses under
each, and only one has ever been scanned. **Target K** (one EC multiply per candidate, cheap):
periodic bit-fills (F1), hex-word fills (F2), counters and sequences (F3), sparse/dense keys
including small integers and the Bitcoin "puzzle" space (F4), ASCII payloads written straight
into the key (F5), structured decimal such as dates and digits of π (F6), and nothing-up-my-
sleeve constants (F8). **Target E** (PBKDF2 plus a derivation tree, ~1000× costlier):
memorable mnemonics — the same word repeated, arithmetic word walks, short word cycles — and
hashed-password entropy (F7). The cost model sets policy: go wide on target K, deep on target E
(enumerate F1 fully to unit width ≤ 24 bits against keys; use dictionaries for F2/F5/F7).

## 5. Measurement infrastructure

A hashed key is indistinguishable from random (§8), so the chain cannot be audited for
"weak-looking" keys — a brainwallet key looks perfect. The only method is to **enumerate the
plausible low-entropy inputs, derive forward across the address fan-out, and intersect with
chain activity.** The opacity is not incidental; it is why enumerate-and-intersect is the sole
approach, and what the scanner (`src/WeakKeyScanner`, .NET 10 / NBitcoin) implements:

- **Derivation fan-out:** BIP-44/49/84/86, compressed and uncompressed, across
  P2PKH/P2SH-P2WPKH/P2WPKH/P2TR; plus brainwallet `SHA256(pw)` P2PKH.
- **Detection:** a local membership test against funded addresses held as 64-bit FNV-1a hashes
  (~1 GB for 59M) — no network, no rate limit — or a direct ever-funded/sweep index built by
  streaming a local `bitcoind` in height order.
- **Funds-neutral guarantee:** no code path constructs, signs, or broadcasts a transaction;
  open source makes it auditable (MISSION.md).
- **Trustworthy nulls:** a `selftest` positive control proves the oracle can emit a true
  positive, so an all-null scan means *swept clean*, not *blind*. It passed against the 59.4M
  set across all four script types: privkey=1 derives to its documented addresses, each type's
  canonical form is byte-identical to the set's and hits the matcher, and a known address hits
  while a garbage string misses.

## 6. Economic analysis

**Compromise events and the four-actor classifier.** A funded weak address is not a victim
until we know how the funds got there and whether a spend was theft. We classify each funded
address from on-chain signals alone. *Sweep latency* is primary: an attacker sweep is a
fee-race, so its latency is ~0; any real latency means nobody was racing, so the owner moved
the funds. *Sweeper reach* corroborates: a destination that empties ≥2 distinct weak addresses
is a serial collector (a bot); a single-use destination is a self-custody candidate. *Flow
shape* rescues active wallets: an address re-funded or held for days is a working wallet even
when some spends are same-day. Overlaid on this are two funder-side classes. A **seeding
cluster** — many addresses funded once, with an identical small amount, in a narrow window — is
a researcher honeypot, not organic use. A **recognizable/famous string** (a keyboard walk, a
run, a published test vector) is a lark: deliberately deposited to, the way people send to
Satoshi's address, not a good-faith wallet. The result is four actors — researcher/honeypot,
general-user custody, larker, and genuine victim — with bad guys being the sweepers. The
classifier is coded (`analyze`) and applied uniformly; we value flows in the money of the day
(deposit- and sweep-time, per a daily price series), never at today's price.

This systematizes what FC16 did by hand — they excluded a researcher seeding campaign and a
stress test, and separated owner from attacker drains (§10); we turn those manual exclusions
into a uniform, reproducible split and apply it to new populations.

**The sweep race taxes theft to miners.** Sweeping is an all-pay fee auction: drainers race to
spend a funded weak UTXO under replace-by-fee, so the winner bids much of the bait away in
fees. Measuring each single-input sweep's fee against its bait (n = 117 on the mnemonic+target-K
set), the median transfer to miners is **16.5%**, concentrated where genuine drains are: for
baits below 0.01 BTC (n = 103) the median is **28%** (value-weighted 16%, tail to ~90%), while
the few baits ≥ 0.01 BTC (n = 14) lose ~1.3% — and those large, lightly-taxed sweeps are
self-custody, not theft (§7). So fee erosion falls hardest on the attacker's real take. Add the
near-zero *marginal* cost of watching an already-enumerated space and a small, declining victim
inflow (§7), and this is an economy **rational only because watching is nearly free** — any
positive residual justifies continuing, while competition bids most of the small-drain value to
miners. For new entrants the residual is plausibly negative; the early bots took the large
early hauls.

## 7. Results

The current balance of the weak-key space is effectively zero, so the loss is historical and
must be read from ever-funded state. Read that way it is real but small: across the mnemonic
and target-K populations, good-faith-victim loss is ≤ \$16k in money-of-the-day; the brainwallet
replication adds ≈\$31k; nothing holds a live balance today. All figures use address index 0,
so each is a lower bound.

### 7.1 The current-balance space is swept clean

A repeated-word live scan against the 59M-address current-balance set (full 12+24-word space,
indices 0–19, 22.3M addresses) returns **0 live hits**, and a CrackStation brainwallet scan
(63.9M passwords → 127.9M addresses) returns **0**. The self-test confirms this is *clean*, not
*blind*. The standing weak-key balance is ~0 — the signature of a continuously-swept space, not
of safety. Present balance is the wrong signal.

### 7.2 Ever-funded prevalence and sweep latency

The full-chain node walk finds **158 funded** mnemonic addresses (127 `repeat`, 31 `forward`;
`backward`/`stride` 0) plus **37 raw target-K** addresses (§7.5), for **195 total**. Where a
sweep happens it is same-day: median latency **0 days in every year 2015–2026** (n = 386, p90 =
1 day, max 279). The drainers are few and concentrated — **230** distinct sweeper addresses,
the top bot draining 17 distinct weak addresses, the next 14, then 7/6/6 — automated
harvesting, not incidental collection.

### 7.3 Who funds these addresses: the four-actor split

Aggregate flow is not loss. Applying the classifier (§6) to the 195 funded addresses, valued in
the money of the day:

| Actor | Addrs | Keys | BTC | USD @ time |
| --- | ---: | ---: | ---: | ---: |
| Good-faith victim (drained) | 68 | 61 | 1.03 | \$15,891 |
| Larker / deliberate (famous string) | 7 | 2 | 0.78 | \$8,143 |
| Single-spend (unattributed) | 75 | 69 | 0.40 | \$11,410 |
| General user (custody, self-moved) | 44 | 39 | 30.77 | \$214,580 |
| Live / unspent | 1 | 1 | 0 | ~\$1 |

**Good-faith-victim loss is at most ≈\$16k** (an upper bound — every key here is
dictionary-guessable, so some "victims" are larks). Most funded *value* is **self-custody**: the
custody row is dominated by one raw key, `0xFACED` (29.79 BTC, ≈ \$190k when it moved in 2018),
which on inspection is owner-controlled, not drained. Value is extreme-concentrated (top-5 keys
= 96.6%, Gini 0.99). At today's price these coins would read ~100× larger and misstate the harm.
No address holds a live balance today (two residual dust UTXOs, 546 and 1,000 sats).

### 7.4 Freshness: do victims still arrive?

Yes. Victim/ambiguous first-fundings are sustained, not declining: 13 (2021), 13 (2022), 15
(2023), 15 (2024), 14 (2025), 10 (2026 partial). Fresh victims keep arriving a decade after the
patterns were public — the space is active, which is the verdict that shapes disclosure (§9).

### 7.5 Raw target-K families

Enumerating the raw-key side over the same chain adds **37 funded** target-K addresses, disjoint
from the mnemonic set:

| Sub-family | Funded addrs | BTC |
| --- | ---: | ---: |
| Periodic fills (F1, unit ≤ 24 bits) | 20 | 29.84 |
| Single-byte fills (F2) | 16 | 0.50 |
| Hex-word fills (F2, e.g. `deadbeef`) | 1 | 0.05 |

The value is ~98% the single `0xFACED` self-custody address (§7.3); the *count* is the result —
these keys are used in practice, with the attacker-swept subset small. Single-byte fills
(`0x11`, `0xbb`) and `deadbeef` were genuinely bot-swept; the wider periodic fills were not.

Extending to the remaining raw families changes little. Counters (F3), ASCII payloads (F5),
structured decimal (F6), and nothing-up-my-sleeve constants (F8) returned **0 funded**. Only the
sparse/small-integer space (F4) is funded — 389 addresses, 12.6 BTC (≈ \$15k at the time),
dominated by privkey=1 (8.14 BTC) — but that is the actively-hunted Bitcoin "puzzle" and
burn-address space: deliberate, not victims, and it serves here as a positive control (the
scanner lights up on privkey=1 as it must).

### 7.6 Brainwallet replication on ever-funded data

The `SHA256(password)`-as-key family is the closest prior art (§10), so we reproduce it on full
ever-funded data rather than the current-balance set of §7.1. From CrackStation (63.9M passwords
→ 127.9M addresses) the walk finds **18,592 funded addresses, 0 live**. The raw count is
inflated by one 2013 seeding campaign (§6); the *organic* population (passwords funded ≥ 10k
sats) is **≈1,249**, the same order as the 884 the original study reported — a replication and
modest extension, not a new headline. Value is extreme-concentrated (top-5 passwords = 64%, Gini
0.99) on trivially-guessable strings (`asdfghjkloiuytrewq`, `deadsheep`, keyboard walks, a
pangram) as plausibly larks as good-faith wallets. The four-actor split:

| Actor | Addrs | Keys | BTC | USD @ time |
| --- | ---: | ---: | ---: | ---: |
| Good-faith victim (drained) | 610 | 596 | 76.69 | \$30,796 |
| Larker / deliberate (famous string) | 282 | 261 | 22.30 | \$12,508 |
| Single-spend (unattributed) | 104 | 103 | 12.23 | \$15,311 |
| General user (custody, self-moved) | 519 | 519 | 18.15 | \$4,535 |
| Researcher / honeypot (2013 seed) | 17,077 | 17,077 | 0.93 | \$116 |

So the raw 96 BTC of "drains" is **≈\$31k of good-faith-victim loss at the time** (upper bound),
sitting on a handful of guessable strings, with a concentrated set of bots harvesting (the top
drainer reached thousands of addresses). The larger BTC totals are self-custody, larks, or one
2013 experiment — not theft.

### 7.7 Memorable phrases: quotes and AI-composed passphrases

A dictionary omits famous quotes, scripture, and pop-culture lines, but a human picking a
memorable secret reaches for exactly those — and a language model generates that distribution
on demand. Testing hand-picked phrases as brainwallets confirms the class is real: the Bitcoin
genesis-block headline, `The Times 03/Jan/2009 Chancellor on brink of second bailout for banks`,
was funded 0.0387 BTC (swept), and `In the beginning God created the heaven and the earth`
0.005 BTC (swept).

Systematically, we ran a public-domain quotation corpus (Wood's 1899 *Dictionary of Quotations*,
29,067 quotes) through `SHA256(pw)`, expanded into the forms a human actually types — casings,
separators, punctuation, first-letter acronyms, and reversals (≈130 per phrase, 6.2M addresses).
The walk found few genuine hits, all dust and all swept: a Latin proverb (`Verba volant, scripta
manent`, 0.027 BTC ≈ \$75), a Shakespeare line, two scripture lines. Their disposition is a
mix — one bot-swept, two ambiguous, two owner-moves. The lesson is exact-string sensitivity:
brainwallet yield depends on matching the *form*, so the class is sparse and variant-driven —
even `to be or not to be` has no funded wallet. The famous copy-pasted strings (the genesis
headline, a Bible verse) hit; arbitrary quotes mostly do not.

## 8. The defense

Two plausible gates are both wrong. **Form-validity** passes every weak seed, since the checksum
certifies structure, not randomness. **Output-key inspection** is the subtler failure: a key
derived by hashing — a brainwallet, an AI-composed phrase, any mnemonic after PBKDF2 — is
computationally indistinguishable from a CSPRNG key, so no entropy, compression, or statistical
test on the key can flag it. (Measured: byte-entropy separates periodic fills and small integers
from random, but `SHA256(password)` and a secure key both sit at the sample maximum.) Output
inspection catches only the raw-structural families; the hashed families, where most victims
are, are opaque.

The weakness is therefore not an artifact to detect but an **affordance to remove**. Deriving a
key from a typed input has exactly one advantage — memorability — and that is the property that
makes it guessable. Anyone with a high-entropy input uses the bytes directly, not a hash of
them, so the passphrase-to-key path does not merely permit weak keys, it *selects* for them: its
only users are the ones it endangers. Retire the affordance rather than inspect its outputs.

What remains, by cost over benefit: (1) **remove the affordance** — tooling should not offer
"type a secret → get a key" derivation; cheapest, at the source; (2) **a poisoned-address
denylist** — for the families you cannot inspect, refuse funding to known-weak addresses; the
strongest honest claim is "not known-weak," never "proven strong"; (3) an **input-side strength
check** on mnemonic entropy and dictionary passwords, which doubles as the VC-exposure detector
that flags checksum-valid mnemonics committed to code (GitHub secret scanning); (4) an **output
structural check** for the raw-structural families — trivial and portable, but the lowest-value
layer.

Plainly, the payoff is small, and we say so. Every layer protects a *self-selected* population:
whoever chose a memorable key is, by definition, the one doing the dangerous thing. Universal
adoption is a large ask; the beneficiaries are a handful of (sometimes well-resourced) users
saved from a self-inflicted mistake. It is closer to "do not sell hammers labelled *hit your own
head*" — retire the affordance, cheap and targeted — than to "everyone wear a helmet." The
economics (§6) bound the upside further: attacker profit is already marginal, most of each small
theft bid away to miners, and fresh-victim inflow small and declining. The contribution is the
honest mapping of which cheap intervention covers which class — not a claim that the sky is
falling.

## 9. Ethics and disclosure

This work observes theft of real funds, so we treat ethics as a design constraint. The full
charter is in MISSION.md; the operative commitments:

**We touch no funds, and the tool cannot.** No transaction is ever signed with a key we did not
generate — no spends, sweeps, dust, warnings, or recovery. This is architectural: no code path
constructs, signs, or broadcasts a transaction. We considered and rejected a "warn the owner
with a tiny send": it is theft of the commingled funds of colliding claimants (§3), and the
full-RBF race means the warning is out-bid and triggers the very sweep it warns of.

**Public data only, minimal retention.** We read public chain state and public dumps, access no
private system, and respect API terms. Findings retain only `(pattern_id, path, address,
ever_funded, first_seen, swept_after)`; we store no mnemonics, seeds, or keys — the pattern id
regenerates a candidate, so storing the secret would only create a target.

**No attribution.** We can say an address is derivable from a low-entropy mnemonic and name the
pattern class; we do not say whose it is. Key collisions (§3) can make an ownership claim
outright false, and a wrong attribution is the one way a read-only project causes harm. We
cluster no funders to identities and report victim counts only as bounds.

**Human subjects.** The study observes public records with no interaction or intervention and
collects no PII beyond public addresses; under the Common Rule it is not human-subjects research.
We will obtain a determination letter where a co-author's institution requires it.

**Disclosure is data-driven.** Whether funded addresses are swept in seconds or sit untouched
for years determines who publication endangers — precisely what §7.2–7.4 measure. Tiers,
descending by impact: (1) library/wallet guidance with no offensive detail; (2) a k-anonymity
address lookup that enumerates no target list; (3) routing specific findings through parties who
can identify owners (exchange compliance, wallet vendors, CERTs); (4) publication of the
vulnerability class with aggregate statistics and no keys. Untouched funds are what publication
could endanger; for those we publish the class without the recipe and route through
intermediaries with delay.

**Dual-use.** Enumerating weak keys is the same act for attacker or defender, and attackers have
done it for a decade (§7). We reduce marginal harm by publishing no wordlists, seeds, or runnable
target list, and by withholding per-finding recipes behind the tiers above.

## 10. Related work

Our approach descends from a decade of weak-key measurement and reuses its methods. Heninger et
al. [@heninger2012psandqs] set the template — enumerate a weak subspace, then scan the world for
keys in it — for network-device keys.

On-chain, Vasek et al.'s *Bitcoin Brain Drain* [@vasek2016braindrain] is our closest ancestor.
They measured `SHA256(password)` brainwallets (884 in use, 2011–2015, all but 21 emptied, median
21 minutes, ~a dozen drainers) and already classified funder/drainer intent: they excluded a
researcher seeding campaign (17,784 wallets funded from 36 linked inputs on 31 Aug 2013) and a
2015 stress test, distinguished owner-initiated from attacker drains, and valued losses at the
day's rate. We reuse their instruments — sweep latency, repeated-drain inference,
compressed/uncompressed fan-out, at-time valuation, and researcher/honeypot exclusion — and
extend them to populations they did not cover (BIP-39 mnemonic patterns, the raw target-K
families, and F7), across the full chain to 2026. The Aug-2013 campaign we detect independently
(34 linked funders) is the same event they documented. Castellucci's `brainflayer`
[@castellucci2015brainflayer] is the offensive counterpart, and Ethercombing / "Blockchain
Bandit" [@ise2019ethercombing] shows the same drainer-cluster concentration on weak Ethereum
keys.

**Weak generation vs. weak choice.** A parallel line studies machine-generated weak keys: Milk
Sad (CVE-2023-39910) [@milksad2023; @cve202339910] enumerated a 32-bit Mersenne-Twister seed
space, and Randstorm [@randstorm2023] covers weak browser-wallet PRNGs. The root cause differs —
a broken RNG, not a human choice — but the funding and sweeping dynamics carry over.

**Adjacent measurement.** Closest in time, a Milk Sad update [@milksad2026update16] catalogs
ever-funded classic brainwallets but does not report the sweep-latency, drainer-concentration,
or arrival-rate dynamics we focus on, nor cover the mnemonic or F7 populations; like Ethercombing
(which characterizes the drainer, not the funder), it drops the funder-intent classification FC16
introduced. Our coded four-actor split (§6) makes that classification uniform and reproducible;
without it, an unfiltered funded-address count is dominated by seeding campaigns and self-custody.
Zhou et al. [@zhou2024keyleakage] measure theft from keys leaked on websites (Ethereum). Two
practitioner efforts bracket our families without measuring outcomes: Lopp [@lopp2024repeatedword]
enumerates the repeated-word subset, and Guiar [@guiar2025entropy] analyzes 24-word entropy
patterns. That F7 is realized in usable tooling [@brain2bip] while weak-key "zoos" like vuke
[@vuke] omit these families is part of why we measure them.

**Economic and clustering methods.** For the funder/sweeper analysis we use common-input-ownership
clustering [@meiklejohn2013fistful], with its known limits; for valuation we follow the
ransomware-economics measurements of Huang et al. [@huang2018ransomware] and Conti et al.
[@conti2019ransomwarepayments], valuing at transaction-time and reporting lower bounds; Brengel
and Rossow [@brengel2018keyleakage] similarly recover key leakage from chain data. The drainer
race is a competitive-bot latency phenomenon of the kind Torres et al. [@torres2021frontrunner]
measured for Ethereum front-running. Systematizations of wallet security [@houy2023wallets;
@erinle2025sok; @homoliak2024sok] and seed-phrase usability [@eleshin2025seedphrases] frame why
users reach for memorable phrases in the first place.

## 11. Limitations

Index 0 only, so every figure is a lower bound; deliberate-deposit contamination bounds "victim"
from above; clustering error; selection and survivorship; a daily (not block-granular) price
model; mempool blindness. The four-actor split is inferential — single-use destinations are
self-custody *candidates*, and lark-vs-victim intent is not resolvable per deposit — so we report
victim loss as an upper bound throughout.

## 12. Conclusion

Memorable BIP-39 seeds are weak in the one way a checksum cannot catch, and the addresses they
produce sit in the same dark forest that emptied the brainwallets before them. Their current
balance is effectively zero — not safety, but the signature of a continuously-swept space — so
the loss is historical. Read from ever-funded state it is **real but small and highly
concentrated**: good-faith-victim loss is low five figures in the money of the day across every
family we measured, most funded value is self-custody or larks, sweeps are same-day races run by
a handful of bots, and no weak address holds a live balance today. Fresh fundings still arrive a
decade on, so this is **ongoing, not a post-mortem** — which licenses a careful disclosure
posture. The magnitude is modest; the contributions are the measurement across new key classes,
the four-actor method that keeps self-custody and larks out of "loss," and the economic result
that the sweep race taxes theft to miners. The defensive takeaway is narrow: form-validity is the
wrong gate, so is output-key inspection, and a denylist is near-total cost for near-zero yield —
the one intervention with a workable cost/benefit is a design choice, **do not offer
memorable-secret-to-key derivation.**

---

## Appendix A — Open science and provenance

The scanner, the positive-control `selftest`, and the analysis code are open source, released as
**`badseed`** (anonymized mirror for review). Every commit declares AI- vs human-authored
content, AI commits embed the driving prompt, and `AUTHORSHIP.md` documents the audit method — so
reviewers can inspect exactly how the manuscript was produced.

## Appendix B — Key figures

| Metric | Value |
| --- | --- |
| Current-balance hits (150M+ addr) | 0 |
| Funded weak addrs (mnemonic + target-K, idx 0) | 195 (158 target-E, 37 target-K) |
| Good-faith-victim loss (mnemonic + target-K) | ≤ 1.03 BTC ≈ \$16k money-of-day (upper bound) |
| Self-custody value (not loss) | 30.77 BTC (dom. by 0xFACED, ≈ \$190k at move) |
| Loss concentration | top-5 keys = 96.6%, Gini 0.99 |
| Sweep latency | median 0 days every year 2015–2026 (n=386, p90 1d, max 279) |
| Distinct drainers | 230; top drained 17 / 14 / 7 weak addrs |
| Fee race → miners (single-input, n=117) | small drains (<0.01 BTC) median 28% / value-wtd 16%; large ~1.3% |
| Fresh victim/amb by year | 13('21) 13('22) 15('23) 15('24) 14('25) 10('26 partial) |
| Brainwallet (CrackStation, ever-funded) | 18,592 funded, 0 live; ≈1,249 organic (≈ FC16 884) |
| Brainwallet good-faith-victim loss | ≈ \$31k money-of-day (upper bound) |
| Live balances (all families) | 0 (two residual dust UTXOs, 546 + 1,000 sats) |
