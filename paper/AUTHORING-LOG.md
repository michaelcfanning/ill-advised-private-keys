# Authoring log

Append-only, human-readable record of every authoring commit. Method and audit
commands: [../AUTHORSHIP.md](../AUTHORSHIP.md). Each entry: date · role · verbatim
prompt · summary · short hash (resolve via `git log` where an entry describes its
own commit).

---

### 2026-08-04 · `ai` · Transparency scheme + Introduction/Background draft

**Prompt (verbatim):**

> my suggestion is that we actually start authoring the paper. we can describe our
> premise, we can refer to the previous research, we can describe our process, our
> economic analysis framework, we can create stubs for our data, and TBD analysis
> sections, and a TDB wrap-up. we know many citations to add as footnotes *and*
> we've done some preliminary analysis we can describe. we need to decide on an
> public OSS name, we will publish all our code and results there. I also have a
> special idea. i'd like to clearly document in commit history the contributions
> from AI to our paper, so that we'll clearly mark the AI revision, the direct
> human edit. we should persist my prompts along with revisions. all this will
> create perfect transparency in the authoring process, and we will note this is
> available for review

Follow-up decisions from the same exchange: OSS name **`badseed`** (chosen by the
human — echoes existing personal "bad seed" poisoned-list repos and our own
poisoned-address denylist deliverable); commit discipline **strict / separate
commits** (chosen by the human).

**Summary:** Added `AUTHORSHIP.md` (the provenance method: role trailers,
verbatim-prompt-in-body, separate-commit discipline, audit commands) and this log.
Drafted the node-independent prose the human asked for: Abstract frame (with
`[DATA]` slots held open), §1 Introduction (dark-forest premise, the measurement
gap over FC16 / Milk Sad, contributions, defense-first stance), §2 Background
(BIP-39/32/44+ derivation, brainwallets, the raw-key-vs-entropy split, sweeping
mechanics), and a §12 Conclusion frame. Converted the paper's inline references to
footnote citations. Recorded the `badseed` name and noted the authoring-provenance
scheme in the Open Science appendix as reviewable.

### 2026-08-04 · `ai` · Prior-art scan + references.bib

**Prompt (verbatim):**

> what's our certainty that this exploration, in fact, has not been performed since
> the brainwallet papers? you've stated so previously, should we complete a more
> thorough search of published papers both for possible citations and also to
> ensure we're contributing something novel/useful

**Summary:** Ran a three-pass literature scan (academic/preprint, grey-lit/tools,
citation-lineage). Verdict: novel — no published work measures our population.
Added `paper/PRIOR-ART.md` (the due-diligence record: triangle framing, adjacency
set to distinguish, the Milk Sad-#16 competitor finding, coverage caveats) and
`paper/references.bib` (verified where possible; `% VERIFY`-flagged where the scan
could not confirm a field against a primary source). Key strategic finding: the
Milk Sad group published an adjacent ever-funded brainwallet measurement in Jan
2026 and is the fast-moving competitor — reinforces the USENIX C1 timing. No
manuscript prose changed in this commit; §10 rewrite and the footnote→`[@key]`
citation migration deferred pending the human's format decision.

### 2026-08-04 · `ai` · De-stake novelty prose, rewrite §10, migrate to Pandoc citations

**Prompt (verbatim):**

> yes go ahead with all of this. i will say, though i've observed a lot of jumping
> up and down over establishing novelty, articulating what's *first* in a paper,
> i'm not very interested in this. i definitely want to anchor what we do in other
> work, connect to it, and add new things. i'm not interested in teasing out our
> novelty or making claims in the text. i simply want to complete the work, present
> and analyze the data in the most useful/cogent way.

Clarified mid-turn: "i've observed that attitude in many academic papers" / "an
intense desire to lay stake to being first" — the register to avoid.

**Summary:** Changed the manuscript register from novelty-staking to anchor-and-
connect per the human's direction. Abstract, §1 ("Neighboring populations" replaces
"The measurement gap"; dropped "Nobody has quantified it"/"previously unscanned"),
Contributions item 1, and §4 (dropped "never-scanned") de-staked. §10 rewritten as
"what we build on" — five threads (weak-key measurement; weak generation vs. weak
choice; recent adjacent measurement; economic/clustering methods; wallets & users)
folding in the new citations (Heninger, Torres, Huang, Conti, Brengel, Ethercombing,
Milk Sad #16, Zhou, Lopp, Guiar, brain2bip, vuke, wallet SoKs, Eleshin). Migrated
all footnote citations to Pandoc `[@key]` against references.bib. Installed pandoc
3.10 and ran a trial `--citeproc` conversion: 0 unresolved-citation warnings,
bibliography renders — the md→LaTeX pipeline is proven.

### 2026-08-04 · `ai+human` · Working title (human-proposed)

**Prompt (verbatim):**

> hm ok how's this BIP39 and the Bad Seeds: Into My Arms.

> yes set it and of course add the hyphen! commit these edits as 'human proposed'?

**Summary:** Set the working title. **Human-authored content:** the title concept
"BIP-39 and the Bad Seeds — Into My Arms" (Michael's, proposed in conversation) — a
riff on Nick Cave and the Bad Seeds with "Into My Arms" read as the swept funds
falling into the drainer's arms. **AI-authored, human-refined:** the descriptive
subtitle "Measuring Sweeping and Loss in the Weak-Mnemonic Key Space" — AI proposed
the base phrasing (incl. "Weak-Mnemonic Key Space"); Michael reordered it to
sweeping-first. The mechanical edits to `PAPER.md`/`DRAFT.md` are AI.

### 2026-08-04 · `ai` · Interim node-walk results into §7 + ledger (autonomous)

**Prompt (verbatim):**

> please keep progressing through analyzing the complete chain. please complete any
> utilities work for that processing. please update the paper on a branch in our
> repo. best wishes. [...] if email is an option, drop a note to
> michael.c.fanning@outlook.com

**Summary:** Autonomous, on branch `feat/nodewalk-ever-funded` while the user is
away. The combined `nodewalk [0,450000]` (mnemonic-pattern union, index 0) finished:
4 funded addresses, all `repeat` family, 100% swept. Ran the new `analyze`
(per-family + sweep-latency). Interim classification: 2 canonical zero-entropy test
vectors → deliberate (0.358 BTC); 2 repeated-single-word mnemonics → victim (0.032
BTC); sweep-latency median 1 d (0 d for 2015–16 non-test); 13 sweepers. Surfaced and
fixed a methodology gap (24-word `abandon…art` vector was scoring as a 0.35 BTC
victim; denylisted it). Added a PRELIMINARY, fully-caveated interim block to §7 and
INTERIM rows to the numbers ledger. Modern era (2016–2026) still processing via the
IBD-watcher-triggered tail; complete-chain analysis + USD utility to follow, then a
push notification. No merge to main.

### 2026-08-11 · `ai` · Complete-chain results (2009–2026) into §7 + ledger

**Prompt (verbatim):**

> hello, let's get parallelization working. we can't ship code that's single
> threaded and takes days to run, want a more solid contribution. [and follow-ups:]
> let's finish the work [...] have a total figure of value of BTC when sweeped in
> aggregate?

**Summary:** After parallelizing `nodewalk` (~16× faster) and finishing the full
walk 0→962,052, ran the complete analysis and replaced the interim §7 block, the
ledger, and the §7.2/7.3/7.5 placeholders with the measured full-chain result:
158 funded weak addresses (127 repeat, 31 forward; backward/stride 0); 100 victims,
1.805 BTC ≈ $48,107 USD-at-time; aggregate swept 2.588 BTC; 230 drainers (top swept
17); sweep-latency median 0 days every year 2015–2026; victim arrivals sustained
~13–15/yr through 2025. Framed honestly: modest dollars, but instant + concentrated
+ ongoing draining — the characterization is the contribution. Limitations stated
(index 0 lower bound, funders uncaptured, one block skipped). No merge to main. Also documented the human-proposed-content pattern
in `AUTHORSHIP.md`. The tagline "your crypto swept into my arms because of your bad
seed" (Michael's) is stashed for the future `badseed` repo README.

### 2026-08-12 · `ai` · Session checkpoint: insight ledger + honeypot proposal + stats CSV

**Prompts (verbatim, three concerns from one session; mapped to files below):**

> generate a list with all insecure private keys observed on disk with corresponding
> stats, as TSV or CSV so I can load into excel  →  `analysis/weakkey_aggregate_stats.csv`
> (NOTE: per-key walk output was not persisted; this file holds the committed §7
> *aggregates* only. The true per-key list requires re-running the walk — task #1.)

> a possible research question is for us to spend money to see who is 'watching'
> certain techniques for insecure key generation (like the backward model) [...] we
> don't need to proceed with our honeypot work yet but a proposal is interesting (and
> let's keep it a proposal)  →  `paper/proposals/honeypot-who-is-watching.md`
> (PROPOSAL ONLY; no funds move; blank stake-ceiling awaits human sign-off.)

> i am interested to know how/whether you are capturing things like 'the human had a
> key insight' relative to 'the AI had a key insight'. this telemetry/narrative is
> very interested for transparency  →  `paper/INSIGHT-LEDGER.md`

**Summary:** Node-independent artifacts authored while preparing for a machine
restart. INSIGHT-LEDGER.md adds an idea-origination axis (`Insight-Origin:
human|ai|joint`) alongside `AUTHORSHIP.md`'s text-authorship trailer, seeded with
this session's human insights (forward-only as a distinct class; "attackers cover
faulty gen techniques users never exercise") and AI contributions. Honeypot proposal
documents the active "who is watching" coverage experiment with a control arm and
binding rules of engagement — parked, not executed. Stats CSV materializes the §7
aggregates for Excel. Program tracked as 6 tasks; next up = author the new generators
(`cycle:k`, F1/F2 target-K, AI-sentence corpus) with no node, then a single combined
bitcoind pass. No merge to main.
