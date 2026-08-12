# Proposal (NOT YET APPROVED): "Who is watching?" — active coverage measurement

> **Status: PROPOSAL ONLY.** No funds move under this document. It exists to be
> reviewed, costed, and either approved with an explicit stake ceiling or declined.
> Nothing here is authorized until the human author signs off in writing. Origin of
> the idea: human (see `INSIGHT-LEDGER.md`, 2026-08-12).

## Motivation

Our on-chain results are entirely **passive**: we read a decade of history and
observe which weak-key techniques were funded and swept. That measures the
intersection of *techniques users produced* and *techniques attackers covered*. It
cannot separate the two. In particular, the `backward` and `stride` families show
**0 funded** — which could mean either "no user ever generated one" or "users did,
but so did we-can't-tell." And it says **nothing** about whether sweepers are
*watching* those families, ready to drain them.

The human's hypothesis (INSIGHT-LEDGER, 2026-08-12): **attacker coverage is broader
than the set of techniques users exercise.** Sweepers may enumerate faulty
generation schemes that no honest user has ever typed. Passive data cannot test
this. An active probe can.

## Design

Derive a **fresh** weak address under a chosen technique, fund it with a **small,
known amount of our own money**, and measure:

1. **Swept or not** within the observation window.
2. **Latency** — time from funding to first spend.
3. **Drainer** — the destination address; cross-reference against the 230 known
   sweepers from §7.5 to see if it is an existing bot or a new one.

Run several techniques **simultaneously**, each with its own address and stake, to
produce a **coverage map**: which faulty-generation classes the watchers actually
cover, and how fast.

### Candidate arms (one address each)

| Arm | Technique | Passive result so far | What a sweep would prove |
| --- | --- | --- | --- |
| A | `backward` (−1 walk) | 0 funded | attackers cover a class no user used |
| B | `stride:2` | 0 funded | same, for a second unused class |
| C | `cycle:2` (two-word) | untested | coverage of period-k cycles |
| D | hex-fill raw key (F2) | untested (target K) | coverage of raw-key fills |
| E | `repeat` (positive control) | 127 funded, same-day | confirms the probe *works* (should drain fast) |

Arm E is the **control**: a technique we *know* is swept same-day. If E is not
drained, the experiment is miswired (wrong address type, funding not visible, etc.)
and no null from A–D is trustworthy.

## Rules of engagement (binding)

These derive from `MISSION.md` and are non-negotiable for this experiment:

1. **Own funds only.** Every satoshi staked is ours. We never fund, touch, sweep,
   or interact with any address we did not create for this experiment.
2. **Expect total loss.** The stake *is* the measurement; a drained arm is a
   successful data point, not a theft against us worth recovering.
3. **Pre-registered stake ceiling.** A fixed maximum total (proposed default:
   **to be set by the human**, e.g. a few dollars per arm) agreed in writing before
   any transaction. No arm exceeds its registered stake.
4. **No entrapment / no third parties.** We do not solicit, lure, or transact with
   any other person. We publish addresses only *after* the observation window, in
   the paper, for reproducibility.
5. **Full disclosure.** Every arm's address, stake, funding tx, outcome, latency,
   and drainer is logged and published. No selective reporting of arms.
6. **Dust-aware.** Stakes must exceed the dust limit and typical sweep fee, or a
   rational bot would ignore them and we would measure fee economics, not coverage.
   Stake sizing is part of the design, not an afterthought.
7. **Reversible until funded.** Deriving and pre-registering addresses commits
   nothing. The point of no return is the first funding transaction, which requires
   explicit human authorization at that moment, not merely approval of this doc.

## Open design questions (for discussion before approval)

- **Stake size vs. attention.** Too small and bots ignore it (we measure the dust
  threshold); too large and we lose real money for the same one-bit answer. Is
  there a known minimum that sweepers bother with? (Our §7 data may bound this — the
  sub-dust vs swept split.)
- **Address visibility.** Sweepers watch the mempool/UTXO set for *funded* weak
  addresses. Do they need the address to have appeared somewhere, or do they
  enumerate the weak space and watch all of it? The answer changes whether merely
  funding is enough to be seen. (Our passive data suggests enumerate-and-watch,
  given same-day drains of freshly-funded addresses — but that is exactly what this
  tests.)
- **Observation window.** §7 latency: median 0 days, p90 1 day, max 279. A window
  of ~30 days captures the mass; a stragglers arm could run longer.
- **Attribution.** If a new (non-§7) drainer appears, that is itself a finding —
  a watcher our historical set missed.

## What this would add to the paper

A section that upgrades the contribution from "here is what was swept" to "here is
the **live coverage frontier** of the sweeper ecosystem" — measured, not inferred.
It also lets us state the `backward`/`stride` nulls precisely: not "unused" but
"[covered | not covered] by watchers as of <date>."

## Decision

- [ ] Approved, with stake ceiling: __________ and window: __________ (human sign-off)
- [ ] Declined / deferred
- [ ] Revise design first: __________
