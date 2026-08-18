# Paper outline

Working title: **BIP-39 and the Bad Seeds — Into My Arms: Measuring Sweeping and
Loss in the Weak-Mnemonic Key Space.** A nod to Nick Cave and the Bad Seeds
(the project ships as `badseed`); read against the topic, "Into My Arms" is the
swept funds falling into the drainer's arms. Carries the FC16 "Bitcoin Brain Drain"
lineage without copying it.

Alternates: *The BIP-39 Brain Drain* (the FC16-lineage version); *In Somebody's
Dictionary: An Economic Measurement of Weak BIP-39 Mnemonics.*

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

**Decision (2026-08-03): target USENIX Security '27 Cycle 1 (25 Aug 2026).** The
draw is USENIX's two-cycle model: a **major-revision** decision invites
resubmission to Cycle 2 (26 Jan 2027) with the *same reviewers* and a concrete
fix-list — so submitting Cycle 1 buys a built-in second attempt with continuity,
which a single accept/reject venue does not offer. Outcomes all favor it: accept →
done at a top venue; major revision → Cycle 2 with a head start; reject → a later
venue. The tradeoff we accept: Cycle 1 overlaps FC'27 (17 Sep 2026) under the
no-concurrent-submission rule, so this **forgoes FC'27** — worth it, since USENIX
is higher-prestige and its revise/resubmit path outweighs FC's FC16-lineage edge.
FC becomes a later-year fallback.

This is a ~3-week sprint for a full-time author. The only real dependency is an
**ever-funded address set** for the arrival-rate result (a current-balance set
can't show funded-then-drained victims); obtain it from a third-party dump rather
than waiting on the local node. If the sprint slips, FC'27 (17 Sep) is the natural
buffer for the same paper. Preprint timing stays subject to the aggregate-first
disclosure posture.

1. **FC'27 — early shot (TARGET).** Financial Cryptography, the direct FC16
   lineage. Accra Beach Hotel, Barbados, 8–12 Feb 2027. Firm paper deadline
   **17 Sep 2026, 23:59 AoE**; notification 5 Nov 2026; final pre-proceedings
   22 Dec 2026. (Associated workshops have an earlier 1 Sep 2026 deadline — a
   fallback if the main measurement isn't ready.) Categories: **Regular 15 pp**,
   **Short 8 pp** ("Short Paper:" prefix), **SoK 20 pp** ("SoK:" prefix), excluding
   references/appendices. Mandatory anonymization, COI disclosure, and a
   no-concurrent-submission attestation.

2. **USENIX Security '27 — top-tier; higher prestige than FC.** Denver,
   11–13 Aug 2027. Two cycles: **Cycle 1 paper 25 Aug 2026** (reg + abstract
   18 Aug, artifacts 28 Aug) and **Cycle 2 paper 26 Jan 2027** (reg + abstract
   19 Jan). Format: **13 pp** body (+refs/appendices), double-blind (anonymous
   artifact links), **mandatory Open Science Appendix** (our open-source scanner +
   `selftest` satisfies this as a strength), ethics appendix strongly encouraged.
   Cycle 1 is a live **stretch target** (~3 weeks) for a full-time author — the
   only real dependency is an ever-funded address set for the arrival-rate result,
   obtainable from a third-party dump without waiting for the local node. Cycle 2
   is the standing fallback, and USENIX's revision/resubmit model extends it.

Later rungs if both miss:

3. **WEIS.** Economics/defense-ROI framing on-topic; friendly to independents.
   Verified Aug 2026: no WEIS 2027 CFP/host/portal yet; WEIS 2026 (UC Berkeley,
   2–3 Jun 2026, deadline 1 Feb 2026) has passed. On the usual cadence the 2027 CFP
   should appear late 2026 with a ~Feb 2027 deadline.
4. **ACM AFT** — strong fit; this cycle's deadline already missed (next-cycle).
5. **Reach — IEEE S&P / CCS / NDSS.**
6. **Preprint — IACR ePrint / arXiv (cs.CR)** to timestamp and gather feedback,
   subject to the disclosure posture (aggregate first, no keys).

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

- **FC'27 runway is tight (~6 weeks to 17 Sep 2026) and the headline arrival-rate
  number is gated on the still-syncing node.** Realistic options, in order:
  (a) a **Short Paper (8 pp)** on the methodology + the mnemonic-space measurement
  (swept-clean result, recon hits) + pre-registered economics, arrival rate framed
  as forthcoming; (b) full **Regular paper** only if the index finishes and the
  economic headline lands in time; (c) an FC **workshop** (1 Sep deadline) as a
  venue for the in-progress measurement. Decide once the node's ETA is known.
- Venue is decided (FC'27); the open sub-decision is Regular vs. Short vs. workshop,
  driven by whether the node-gated arrival-rate headline lands before 17 Sep 2026.
- Solo vs. one academic co-author.
- Preprint timing vs. disclosure posture (aggregate-first constraint).
- How much of the poisoned-address list is publishable vs. routed through
  intermediaries (MISSION.md tiers 2–3).
- Legal review before publication (MISSION.md open question).
