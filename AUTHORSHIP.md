# Authorship and AI-contribution provenance

This repository and the paper it produces are written by a human author working
with an AI assistant (Claude, Anthropic). We record **who wrote what, and from
what instruction, at the granularity of a single commit** — so that any reviewer
can reconstruct the authoring process exactly rather than take our word for it.
This document is the method; it is offered for inspection and is referenced from
the paper's Open Science appendix.

We adopt this because AI assistance in scientific writing is usually invisible in
the final artifact. Making it fully auditable is cheap, honest, and — for a paper
whose subject is measurement integrity and trustworthy nulls — in keeping with the
work.

## Roles

Every commit that touches paper or documentation content declares one role via a
git trailer:

| Trailer | Meaning |
| --- | --- |
| `Author-Role: ai` | The AI generated the change from a prompt. The prompt is embedded verbatim in the commit body. |
| `Author-Role: human` | The human wrote or edited the change directly, by hand. No prompt. |
| `Author-Role: ai+human` | A single commit genuinely mixes both. Avoided where possible (see discipline below); when unavoidable, the body says which parts are which. |

Code commits use the same trailers; the discipline matters most for prose.

## Discipline: separate commits

**AI-generated drafts and human hand-edits land as separate commits.** This is the
rule that makes the tags meaningful: because a human edit never rides inside an AI
commit (or vice versa), a reviewer can `git diff` one commit against the next and
see *exactly* what each party contributed. The normal rhythm is:

1. The AI commits a draft or revision — `Author-Role: ai`, prompt in the body.
2. The human reviews, hand-edits, and commits those edits separately —
   `Author-Role: human`.

The human is free to reject, rewrite, or discard any AI commit; that too is visible
in the history.

## Verbatim prompts

Each `ai` (or `ai+human`) commit embeds, in its message body, the **exact**
prompt(s) that drove *that* change — not paraphrased, not cherry-picked. Where one
prompt spanned several topics, the commit quotes the portion that produced the
committed change and nothing is silently omitted from the intent. The prompt is
bound immutably to the diff it produced, which is the tightest possible record of
input → output.

## Git author of record

To keep accountability clear, the human (Michael C. Fanning) remains the git
**author and committer** of every commit — he is the accountable author of the
submission. AI authorship is denoted by the `Author-Role: ai` trailer and a
`Co-Authored-By: Claude …` trailer, not by rewriting the git author field. The
role trailer, not the author field, is the source of truth for who wrote the text.

## The human-readable log

`paper/AUTHORING-LOG.md` is an append-only narrative index: one entry per authoring
commit — date, role, the quoted prompt, a one-line summary, and the short hash.
It is what a reviewer reads to follow the process without doing git archaeology;
the commit trailers are what a reviewer greps to audit it mechanically. The two
must agree.

## Auditing this repository

```sh
# Every AI-authored commit, newest first:
git log --format='%h %ad %s' --date=short --grep='Author-Role: ai'

# Every direct human edit:
git log --format='%h %ad %s' --date=short --grep='Author-Role: human'

# The full message (including the verbatim prompt) for one commit:
git show -s --format='%B' <hash>

# What the human changed on top of a specific AI draft:
git diff <ai-commit> <the-human-commit-that-follows-it>
```

## Scope and history

This scheme begins at the commit that introduces this file. Commits before it were
authored by Michael with `Co-Authored-By: Claude` where the AI contributed, but
were not separated by role; they are not retroactively re-tagged (published history
is not rewritten). From here forward the discipline above is in force.
