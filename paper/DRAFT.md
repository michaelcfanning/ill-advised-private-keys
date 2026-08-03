# DRAFT — The BIP-39 Brain Drain (working draft)

Content-first draft in markdown; port to the USENIX LaTeX template (13 pp body,
anonymized) before submission. Structure follows [../PAPER.md](../PAPER.md);
methodology is [../ECONOMICS.md](../ECONOMICS.md); charter [../MISSION.md](../MISSION.md).

Status tags: **[HAVE]** verified this project · **[REPRO]** measured before, must
reproduce with exact figures · **[DATA]** blocked on the ever-funded set ·
**[TODO]** unwritten. Never let a [REPRO]/[DATA] number reach the abstract until
it is [HAVE].

---

## Abstract [TODO]

One paragraph: the population (BIP-39 memorable-mnemonic keys + SHA256(pw) as
BIP-39 entropy), the headline loss/latency/drainer numbers, the freshness verdict
(are victims still arriving?), and the shipped defense. Write last, once the
numbers are [HAVE].

## 1. Introduction [TODO]

- The dark-forest premise; attackers have farmed weak keys for a decade.
- The measurement gap: FC16 measured brainwallets; nobody has measured the BIP-39
  mnemonic-pattern population or the SHA256(pw)-as-entropy family.
- Contributions (see below).
- Defense-before-disclosure stance.

### Contributions
1. Measurement of a new population: BIP-39 mnemonic-pattern keys + SHA256(pw) as
   BIP-39 entropy (family F7), disjoint from a decade of brainwallet scanning.
2. A freshness result: arrival rate of fresh weak-key victims over time. **[DATA]**
3. Attacker economics: on-chain sweep-fee measurement → a profitability threshold,
   reframed as a defensive design parameter. **[DATA]**
4. A shipped defense: a non-blocking BIP-39 strength check + a poisoned-address
   denylist, contributed upstream.
5. An auditable, funds-neutral methodology: an open-source scanner that provably
   cannot move funds, with a pre-registered economic analysis.

## 2. Background [TODO]

BIP-39 (entropy + checksum validates form, not randomness); BIP-32/44/49/84/86
derivation; brainwallets; the raw-key (target K) vs entropy (target E) split.
Condense from [../PATTERNS.md](../PATTERNS.md).

## 3. Threat model [HAVE, from MISSION.md]

Attack cost scales with the size of the weak-key space, not the victim count: one
pass over the enumerated space covers every user who made the mistake. "Weak = in
somebody's dictionary." The user memorizes a *procedure*, not a phrase, so the real
entropy is the length of the shortest description the author would use — a dozen
bits, regardless of the nominal 128/256.

## 4. Weak-key families [HAVE, condense PATTERNS.md]

Periodic fills (F1), hex-word fills (F2), counters/sequences (F3), sparse/dense
keys (F4), ASCII payloads (F5), **hashed-password entropy (F7 — the highest-value,
never-scanned family)**, nothing-up-my-sleeve constants (F8). Cost model → policy:
go wide on target K, deep on target E. Table T2.

## 5. Measurement infrastructure [HAVE]

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

## 7. Results

### 7.1 The current-balance space is swept clean [HAVE]
- Repeated-word LIVE scan, Loyce 59M current-balance set, full 12+24-word space,
  indices 0–19 (22.3M addresses): **0 live hits**.
- CrackStation brainwallet scan: **63,941,068 passwords → 127,882,136 addresses,
  0 hits**, 1,299.5 s (~49.5k pw/s, 36 threads). Self-test confirms this is *clean*,
  not *blind*.
- Interpretation: the standing weak-key balance is ~0 — consistent with a space
  swept continuously. Present balance is the wrong signal; the loss is historical
  and must be read from ever-funded state, not current balance.

### 7.2 Ever-funded prevalence and sweep latency [DATA]
Requires the ever-funded set (Blockchair dump). Per-family: fraction of the
enumerated space ever funded, fraction swept, sweep-latency distribution (Fig F1).

### 7.3 Freshness — do victims still arrive? [DATA]
Arrival rate of first-funding per period (Fig F2). The verdict that sets the
disclosure posture.

### 7.4 Confirmed hits (recon) [REPRO]
Prior API recon surfaced real funded-then-swept weak keys (e.g. repeated-word
`act`×11 + `abandon`; a 24-word `abandon…art` variant reported ≈0.35 BTC; the
brainwallet control `correct horse battery staple` ≈21.9 BTC gross, classified
deliberate-published). **Reproduce every figure exactly before use; treat famous
keys as deliberate, not victim.**

### 7.5 The drainers [DATA]
Sweeper-cluster concentration, longevity, cumulative gain (Fig F3). Do they race?

## 8. The defense [HAVE design]

Dual detector sharing a regex-then-checksum core: VC-exposure (GitHub secret
scanning) fires on any checksum-valid mnemonic; wallet import blocks only
low-entropy ones, on entropy-compressibility, non-blocking. Plus the
poisoned-address denylist, seeded in `Econ.SeedDenylist`.

## 9. Ethics and disclosure [HAVE, from MISSION.md]

Funds-neutral (touches nothing, cannot sign); public data only; minimal retention
(pattern_id, not seeds); never asserts attribution; defense ships before
disclosure; tiered disclosure posture; why "warn with a transaction" is rejected.
This section is node-independent — write it in full now.

## 10. Related work [TODO]
Vasek et al. FC16; Castellucci (brainflayer, DEF CON 23); Milk Sad (weak RNG);
address-clustering; blockchain economic-measurement literature.

## 11. Limitations [HAVE, from ECONOMICS.md]
Deliberate-deposit contamination; lower-bound framing (only enumerated patterns);
clustering error; selection/survivorship; price model; mempool blindness.

## 12. Conclusion [TODO]

---

## Open-science + ethics appendices (USENIX-mandatory) [TODO]
- Open Science Appendix: the scanner is open source; state the artifact and how to
  access it (anonymized for review).
- Ethics appendix (strongly encouraged): the funds-neutral guarantee and disclosure
  posture.

## Numbers ledger (single source of truth — fill as measured)
| Metric | Value | Status | Source |
| --- | --- | --- | --- |
| Repeated-word live hits (22.3M addr) | 0 | HAVE | out/live_indices20.log |
| CrackStation passwords scanned | 63,941,068 | HAVE | out/brain.scan.log |
| CrackStation addresses / hits | 127,882,136 / 0 | HAVE | out/brain.scan.log |
| Self-test | PASS (8/8, 4 addr types) | HAVE | selftest vs Loyce 59.4M |
| Ever-funded prevalence per family | — | DATA | Blockchair dump |
| Arrival rate by year | — | DATA | Blockchair dump |
| Total victim loss (USD-at-time) | — | DATA | dump + price series |
| Distinct drainer clusters | — | DATA | dump |
| Sweep-latency median | — | DATA | dump |
