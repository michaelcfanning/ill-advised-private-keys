# Paper outline

Working title: **The BIP-39 Brain Drain: Measuring Loss and Sweeping in the
Memorable-Mnemonic Key Space.**

Alternate: *In Somebody's Dictionary: An Economic Measurement of Weak BIP-39
Mnemonics and a Defense That Ships First.*

This is the publication skeleton. Charter is [MISSION.md](MISSION.md), enumerable
spaces are [PATTERNS.md](PATTERNS.md), and the economic methodology is
pre-registered in [ECONOMICS.md](ECONOMICS.md). Nothing here is anonymized yet;
anonymize before submission (see *Logistics*).

## Thesis

Attackers have farmed the weak-key space for a decade, but the defensive side is
unmeasured for BIP-39 mnemonic-pattern keys specifically. We measure it — losses,
sweep latency, drainer population, and whether fresh victims still arrive — and
we ship the defense (a strength check and a poisoned-address list) on aggregate
statistics before disclosing specifics.

## Contributions (the delta over prior art)

1. **A new population.** FC16 measured brainwallets (`SHA256(pw)` as a *raw key*).
   We measure **BIP-39 mnemonic-pattern** keys — a later era, a different
   derivation scheme, a different user — plus the previously unscanned family of
   `SHA256(pw)` used as BIP-39 *entropy* (PATTERNS.md F7).
2. **Freshness.** An arrival-rate series answering whether weak-key victimization
   is ongoing or historical — the question that decides disclosure.
3. **Attacker economics.** On-chain sweep-fee measurement yielding a
   profitability threshold, reframed as a defensive design parameter.
4. **A shipped defense.** A non-blocking BIP-39 strength check and a
   poisoned-address denylist, contributed upstream — defense before disclosure.
5. **An auditable, funds-neutral methodology.** An open-source scanner that
   provably cannot move funds, with a pre-registered economic analysis.

## Target venues

Ranked by fit, not prestige:

1. **Financial Cryptography and Data Security (FC).** The direct lineage — FC16 is
   the paper we extend. Primary target.
2. **WEIS (Workshop on the Economics of Information Security).** The economics and
   defense-ROI framing is squarely on-topic; friendly to independent researchers.
3. **ACM AFT (Advances in Financial Technologies).** Strong crypto/measurement fit.
4. **Reach — USENIX Security / IEEE S&P / CCS / NDSS.** Competitive but open to
   independents; USENIX publishes substantial measurement work. Consider after an
   FC/WEIS-hardened draft.
5. **Preprint first — IACR ePrint and/or arXiv (cs.CR),** to timestamp and gather
   feedback, subject to the disclosure posture (aggregate first, no keys).

## Logistics for an independent submitter

- **Affiliation:** "Independent Researcher" (optionally "formerly Microsoft"). A
  standard, respected byline; 35 years on secret-scanning is credibility.
- **Double-blind:** most targets are double-blind. Strip author identity;
  anonymize the repo/artifact link (an anonymized mirror or a redacted archive).
  Avoid self-identifying phrasing.
- **Ethics section (mandatory at S&P/USENIX/CCS, expected everywhere):** lead with
  the funds-neutral guarantee, read-only observation, minimal retention, no
  attribution, and the disclosure tiers. This is a project strength — foreground
  it, don't bolt it on.
- **Human-subjects / IRB:** we collect no PII beyond public addresses and make no
  deanonymization attempt; document that explicitly. If a co-author is at a
  university, their IRB may still want a determination letter.
- **Artifact evaluation:** submit the scanner for an Artifact-Available/Functional
  badge; the self-test (`selftest`) and positive controls make it reproducible.
- **Co-author (optional):** a recent-academic co-author eases review logistics and
  is reachable through existing GitHub/Microsoft contacts, but is not required.

## Section-by-section

1. **Abstract.** Population, headline loss/latency/drainer numbers, freshness
   verdict, the shipped defense. (Write last.)
2. **Introduction.** The dark-forest premise; the measurement gap for BIP-39
   patterns; contributions; the defense-before-disclosure stance.
3. **Background.** BIP-39 (entropy + checksum validates form, not randomness),
   BIP-32/44/49/84/86 derivation, brainwallets, the raw-key vs entropy split
   (PATTERNS.md targets K and E).
4. **Threat model.** Attack cost scales with the key space, not the victim count;
   "weak = in somebody's dictionary"; the memorized-procedure entropy argument.
5. **Weak-key families.** Condensed from PATTERNS.md: periodic fills, hex-word
   fills, sequences, sparse/dense keys, ASCII payloads, hashed-password entropy,
   nothing-up-my-sleeve constants; the cost model and enumeration policy.
6. **Measurement infrastructure.** The scanner: derivation fan-out, the offline
   FNV oracle, enrichment, parallelism; the funds-neutral architectural guarantee;
   the self-test / positive controls that make null results trustworthy.
7. **Economic analysis.** Per ECONOMICS.md — the compromise-event unit, the
   victim/deliberate taxonomy, losses valued two ways, attacker economics and the
   profitability threshold, unique-victim bounds, sweeper population, and the
   arrival-rate series.
8. **Results.** Losses, sweep-latency distribution, drainer concentration, per-
   family saturation, arrival rate. All lower bounds over the defined universe.
9. **The defense.** The dual detector (VC-exposure fires on any checksum-valid
   mnemonic; wallet-import blocks only low-entropy ones), entropy-compressibility
   as the signal, and the poisoned-address list; upstream contribution path.
10. **Ethics and disclosure.** The binding principles; the tiered disclosure
    posture; why "warn with a transaction" is rejected; collision/ownership and
    non-attribution.
11. **Related work.** Vasek et al. FC16 (brain drain), Castellucci (brainflayer,
    DEF CON 23), Milk Sad (weak RNG), address-clustering literature, blockchain
    economic-measurement papers.
12. **Limitations.** The threats to validity from ECONOMICS.md, verbatim.
13. **Conclusion.** Whether the space is fresh, what the defense prevents, what to
    ship.

## Planned figures and tables

- **F1** Sweep-latency CDF, by era — the headline "harm is fast" figure.
- **F2** Arrival rate of fresh weak-address funding per period — the freshness
  verdict.
- **F3** Drainer concentration (Lorenz / top-k share of swept value).
- **F4** Per-family saturation: fraction of the enumerated space ever funded, and
  fraction swept.
- **F5** Attacker fee vs. deposit size, with the profitability-threshold fit.
- **T1** Loss totals under each victim-classification rule (victims / +ambiguous /
  all), in BTC and USD-at-time.
- **T2** Weak-key families with candidate counts and derivation cost.

## Positioning against FC16

Same headline metrics (latency, repeated drains per family, compressed/
uncompressed fan-out) so the comparison is legible; a disjoint population
(BIP-39 patterns + hashed-password entropy) so the numbers are new; and a forward
contribution FC16 did not make — a shipped, non-blocking defense and a
poisoned-address list grounded in the measurement.

## Open decisions

- Primary venue: FC vs. WEIS (deadlines drive this — build the deadline calendar).
- Solo vs. one academic co-author.
- Preprint timing vs. disclosure posture (aggregate-first constraint).
- How much of the poisoned-address list is publishable vs. routed through
  intermediaries (MISSION.md tiers 2–3).
- Legal review before publication (MISSION.md open question).
