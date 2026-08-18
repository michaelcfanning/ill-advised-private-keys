# Insight ledger — who originated which idea

`AUTHORSHIP.md` records **who wrote which text**, at commit granularity, with the
driving prompt bound verbatim to the diff. That captures *authorship of prose and
code*. It does **not** cleanly capture a different axis the human author cares
about: **who originated a key idea** — the conceptual turns, hypotheses, and
research directions — as distinct from who typed the sentence that recorded it.

This file adds that axis. It is deliberately subjective and hand-maintained: an
honest narrative of intellectual provenance, not a mechanical trailer. It is
referenced from the Open Science appendix alongside `AUTHORSHIP.md`.

## Why a separate axis

Text authorship and idea origination come apart constantly:

- A human can articulate a hypothesis in one sentence in chat; the AI then writes
  three paragraphs of analysis around it. The *paragraphs* are AI-authored
  (`Author-Role: ai`), but the *insight* is the human's.
- The AI's scanner can surface a number (e.g. "31 forward-family addresses were
  funded") that neither party anticipated; recognizing that this number
  constitutes a **distinct phenomenon worth naming** can be the human's insight.

Under `AUTHORSHIP.md` alone, idea origin is only recoverable by reading the quoted
prompt and inferring. This ledger makes it explicit.

## Proposed convention (optional trailer)

For commits that turn on a specific idea, an optional trailer names its origin,
independent of `Author-Role`:

```
Insight-Origin: human    # the conceptual move originated with the human
Insight-Origin: ai       # the conceptual move originated with the AI
Insight-Origin: joint    # genuinely co-developed in dialogue
```

`Author-Role` still records who wrote the committed text; `Insight-Origin` records
whose idea it was. A commit can be `Author-Role: ai` + `Insight-Origin: human`
(AI wrote up the human's idea) or `Author-Role: ai` + `Insight-Origin: ai` (AI's
own idea, AI-written), and the pair is the honest record.

## Ledger

Newest first. Each entry: date · origin · the idea, in one line · where it landed.

### 2026-08-12

- **`human`** — **"Forward-only is a distinct, previously-undiscussed class."**
  The scanner (AI) produced the count of 31 funded `forward`-family addresses; the
  human recognized that a sequential-wordlist mnemonic being funded-and-swept is a
  *categorically different* signal from the repeated-word case, and worth naming as
  its own class. → to be written into §7.5.

- **`human`** — **"Attackers have considered faulty key-generation techniques that
  users don't actually exercise."** Hypothesis: the sweeper population's coverage
  is broader than the set of techniques any human victim uses — so `backward` /
  `stride` showing 0 funded (no user produced them) says nothing about whether
  attackers *watch* them. This reframes the 0-null as "coverage unknown, testable"
  and directly motivates the honeypot proposal. → §7.5 + `proposals/`.

- **`human`** — **Period-k word cycles** (`abandon act abandon act …`) as an
  untested user pattern. A generation technique not covered by the existing
  single-start arithmetic families. → task #2, `cycle:k`.

- **`human`** — **Active "who is watching" measurement**: spend our own funds to
  probe which techniques the sweepers cover, per technique. → honeypot proposal.

- **`human`** — **AI-composed grammatical sentences** drawn from the 2048-word
  list as a plausible "clever user" technique. → task #4.

- **`ai`** — Observation that period-k cycles are **not expressible** in the
  current single-start `PatternSpec` model and need a new k-tuple enumeration path;
  and the **cost/tractability analysis** (full-free period-2/12-word ≈ 327 GB of
  addresses → must bound to index-0 canonical completion). → task #2 scoping.

- **`ai`** — Flagged that the complete-chain **per-key walk output was not
  persisted** (only aggregates survived in §7) and that the "all swept" claim has a
  **1-UTXO exception** that the current-balance scan's threshold could hide. →
  task #1.

- **`joint`** — Index-0 reasoning: human asked whether independent users favor
  index 0; AI supplied the BIP-44 first-receive-address mechanism and the
  victim-vs-attacker index-coverage asymmetry. → §7 limitations framing.
</content>
