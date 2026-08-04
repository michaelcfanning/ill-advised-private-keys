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

## Abstract [FRAME — numbers DATA]

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
question that governs disclosure, whether fresh victims still arrive. We find
[the current-balance space swept clean: 0 hits across 150M derived addresses], and
[DATA: ever-funded prevalence, median sweep latency, drainer concentration,
arrival-rate verdict]. We ship the defense before the specifics: a non-blocking
BIP-39 strength check and a poisoned-address denylist, contributed upstream. All
code, the positive-control self-test that makes our null results trustworthy, and
a fully auditable record of the authoring process accompany the paper.

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

> **Interim node-walk result — PRELIMINARY (this project, branch, in progress).**
> Scope: the mnemonic-pattern union (repeat/forward/backward/stride, 12+24 word),
> address index 0, over block heights 0–450,000 (2009–Dec 2016); the modern era
> (2016–2026) is still processing. Source: our `nodewalk` ever-funded + sweep index
> over a local fully-synced bitcoind — not a third-party dump — validated against
> Blockchair on the first-ever Bitcoin transaction (§5). BTC-denominated, with
> USD-at-time from a daily price series (`data/btc_usd_daily.csv`, blockchain.info):
> interim victim loss ≈ **$22** — negligible at this scale, dominated later by the
> modern era.
>
> - **Ever-funded:** 4 distinct funded addresses, **all in the `repeat` family**
>   (forward/backward/stride: 0 funded in this era), **100% swept** (no residual
>   balance) — consistent with continuous draining.
> - **Classification:** 2 are canonical zero-entropy test vectors
>   (`abandon×11+about`, `abandon×23+art`) → deliberate (0.358 BTC); 2 are
>   repeated-single-word mnemonics (`all×12`, `life×12`) → victim (**0.032 BTC**).
>   The 24-word test vector alone held 0.35 BTC and would have dominated a naive
>   "victim" total — the famous-key trap (§7.4); it was moved to the denylist on
>   discovery.
> - **Sweep latency:** median 1 day overall; the 2015–2016 non-test addresses were
>   swept **within 0–1 day** (same/next-day drain).
> - **Drainers:** 13 distinct sweeper addresses across 9 sweep events.
> - **Limitations (interim):** tiny N at index 0 through 2016; the node walk does
>   not yet capture funders (reported funders = 0 by construction, not a true zero);
>   victim-vs-deliberate for repeated-single-word mnemonics is genuinely ambiguous
>   and pending denylist/behavioral curation. Numbers will change as the modern
>   era and higher address indices are processed.

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

## 12. Conclusion [FRAME — verdict DATA]

Memorable BIP-39 seeds are weak in the one way a checksum cannot catch, and the
addresses they produce sit in the same dark forest that emptied the brainwallets
before them. We measured that this population's *current* balance is effectively
zero — [0 hits across 150M derived addresses] — which is not safety but the
signature of a space swept continuously; the loss is historical and lives in
ever-funded state, not present balance. [DATA: the headline — ever-funded value
lost, median sweep latency, drainer concentration — and the freshness verdict:
whether first-fundings of weak addresses are still occurring, which determines
whether this is a post-mortem or an ongoing harm.] Either way the defensive
conclusion is the same and shippable now: form-validity is the wrong gate, so
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
| Ever-funded (0–450k, idx0, union) | 4 addr, all `repeat`; fwd/bwd/stride 0 | INTERIM | node.findings.jsonl |
| Victim loss BTC (interim 0–450k) | 0.032 BTC (2 addr; USD node-gated) | INTERIM | node walk |
| Deliberate/test-vector BTC (interim) | 0.358 BTC (2 zero-entropy vectors) | INTERIM | node walk |
| Distinct sweepers (interim 0–450k) | 13 over 9 sweep events | INTERIM | node walk |
| Sweep-latency median (interim) | 1 d overall; 0 d for 2015–16 non-test | INTERIM | node.latencies.tsv |
| Arrival victim/amb (interim) | 2015: 1, 2016: 2 | INTERIM | node walk |
| Ever-funded prevalence per family (full) | — | DATA | full-chain node walk |
| Total victim loss USD-at-time (full) | — | DATA | node walk + price series |
