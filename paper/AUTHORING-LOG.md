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
