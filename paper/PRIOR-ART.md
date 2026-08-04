# Prior-art / novelty scan

Due-diligence record behind the paper's novelty claim. Result of a three-pass
literature scan on 2026-08-04 (peer-reviewed venues + preprints; grey literature,
talks, and tools; citation lineage around FC16 / Milk Sad / Meiklejohn /
brainflayer). Citations live in [references.bib](references.bib). This is our own
record; not for the manuscript verbatim, but it seeds §10.

## Verdict

**Novel.** No published work — academic or practitioner — measures our population:
memorable / low-entropy BIP-39 *mnemonics* (repeated word, wordlist-in-order,
sequences, byte-pattern fills, ASCII payloads) **and** the SHA256(password)-as-
BIP-39-*entropy* family (F7), with on-chain metrics (ever-funded, sweep latency,
drainer concentration, fresh-victim arrival). The defensible gap is the
**combination** of enumeration breadth + the F7 family + quantified theft dynamics.

Position the work as the third corner of a triangle:

| Corner | Population | Published |
| --- | --- | --- |
| Direct brainwallets | `SHA256(pw)` **as the private key** | Vasek et al., FC 2016 |
| Weak-RNG BIP-39 | broken PRNG generates the entropy | Milk Sad / CVE-2023-39910 |
| **Ours** | **human-memorable BIP-39 mnemonic + `SHA256(pw)` as entropy** | **unoccupied** |

The reviewer risk is *framing*, not priority: "this is just brainwallets / Milk Sad
again." §10 must distinguish on the **derivation path**, not the population alone.

## Works to cite and distinguish (adjacency set)

| Work | What it is | Why it is not us |
| --- | --- | --- |
| **Milk Sad Update #16** (Reitter, Jan 2026) | Ever-funded/drained measurement of *classic* SHA256-as-key brainwallets: 20,397 funded addrs, ~3,606 BTC throughput, a Feb-2025 ~$500k wallet swept in seconds. | Same team, same oracle method — but explicitly excludes BIP-39 mnemonics and F7, and explicitly does **not** measure sweep latency, drainer concentration, or arrival rate. **Our four core metrics remain unscooped.** |
| **Zhou et al.** (WWW'24 Companion) | "First measurement study" of theft from keys leaked on websites, on Ethereum. | Ethereum, website-leaked secrets — not mnemonics, no BIP-39 entropy analysis. **Scope our "first" claim to Bitcoin BIP-39 weak-mnemonic/password-seed populations** so the claims don't collide. |
| **Lopp** (blog, Aug 2024) | Enumerates valid repeated-word mnemonics (130× 12-word, 11× 24-word); notes several "used but empty." | Enumeration of one slice of our taxonomy; **no funding/drain data** (anecdote). |
| **Guiar** (Zenodo, 2025) | Byte-level entropy/repetition analysis of 24-word phrases (0x00/0x55/0xAA/0xFF fills). | Structural only; **no on-chain measurement.** Motivates our gap. |
| **Ethercombing / "Blockchain Bandit"** (ISE, 2019) | Weak/truncated ECDSA key scan on Ethereum; single drainer cluster traced. | Raw weak keys, not mnemonics; Ethereum; industry report. Precedent for our **drainer-cluster** metric. |
| **brain2bip** (tool) | Implements `SHA256(pw)` → BIP-39 entropy → mnemonic. | Existence proof that **F7 is real and used in the wild**; generation only, no measurement. |
| **vuke** (tool) | Best public weak-key-family taxonomy (9 families). | **Our two families are absent** — confirms the gap. |

## Strategic finding: a fast-moving competitor

The Milk Sad group is the one actor with both the method (an ever-funded oracle,
identical in spirit to our `bcscan`) and the momentum (actively publishing in this
terrain as of Jan 2026) to close our gap before we do. Every pass independently
concluded: **move fast.** Implications:
- Reinforces holding the **USENIX Security '27 Cycle 1** line (25 Aug 2026) over
  slipping to a later venue.
- Argues for an **early timestamped preprint** to establish priority — *subject to*
  the aggregate-first disclosure posture (no keys, no runnable target list;
  MISSION.md). Decide preprint timing against that constraint deliberately.

## Coverage and caveats

- 3 passes, ~60 distinct queries across Scholar, Semantic Scholar, DBLP, IACR
  ePrint, arXiv, ACM DL, IEEE, USENIX, FC, plus blogs, talk archives, GitHub,
  and forums. Pages fetched directly where possible.
- **Not** exhaustively covered: private Telegram/Discord drainer channels, every
  GitHub repo, non-English venues, very recent (2026) unindexed preprints. Verdict
  is strong-but-not-absolute; the honest fallback claim if a fragment surfaces is
  "first *rigorous / peer-reviewed* measurement," which still holds.
- Several `references.bib` fields are `% VERIFY`-flagged (BIP header author/dates,
  CVE wording, a few DOIs/URLs recovered via secondary sources). Resolve before the
  submission freeze.
