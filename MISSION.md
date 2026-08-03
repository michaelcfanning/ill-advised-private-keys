# Mission

This project measures a class of cryptocurrency wallet compromise: private keys
derived from mnemonics a human constructed to be memorable rather than random. We
enumerate those mnemonics, check whether the resulting addresses have ever held
value, and publish what we learn in a form that helps wallet software prevent the
next occurrence.

We take nothing from a compromised wallet. The tool contains no code that can move
funds, and being open source makes that verifiable.

## The vulnerability

BIP-39 encodes a mnemonic as entropy plus a checksum. The checksum constrains
which final words are legal, which is what lets a hand-built phrase pass as
legitimate. It validates form, not randomness.

A user can repeat a single word and search for a valid final word. The phrase
passes every check a wallet performs, and is trivially enumerable:

- **12 words:** 11 repeated words fix 121 of 128 entropy bits. 128 valid final
  words per prefix: 2048 x 128 = 262,144 phrases.
- **24 words:** 23 words fix 253 of 256 bits. 8 valid final words per prefix:
  2048 x 8 = 16,384 phrases.

Under 280,000 candidates for the simplest pattern; seconds on a laptop.

Attack cost scales with the size of the weak key space, not the number of
victims. An attacker derives each candidate to an address and tests it against
the public set of funded addresses — one pass covers every user who made the
mistake. We therefore assume the space is already farmed.

### What makes a key weak

A key is weak if it is reachable by an enumeration someone would plausibly run —
a property of attacker behavior, not of the key. The operative definition is "in
somebody's dictionary." Independent users reaching for a memorable value converge
on the same small set, so the patterns worth enumerating are the ones people
converge on, not the mathematically elegant ones.

### The user memorizes a procedure, not a phrase

Someone building a memorable seed memorizes a short rule that regenerates the
words: "repeat abandon," "start at word 400 and count up," "every other bit set."
The search space is therefore a set of short programs from a human-memorable
instruction set, each with a small parameter space. The real entropy is the
length of the shortest description the author would use — a dozen bits or so,
regardless of the nominal 128 or 256.

Program families (specified with sizing and cost in [PATTERNS.md](PATTERNS.md)):

- **Repetition.** Repeat word *n*. 2048 parameters.
- **Sequence.** Start at word *n*, walk the wordlist. 2048 parameters.
- **Stride.** Start at *n*, step by *k*. ~4.2M before checksum.
- **Index arithmetic.** Powers of two, primes, Fibonacci, round numbers.
- **Bit patterns on the entropy.** Alternating bits, 8-on/8-off, repeating bytes,
  ascending bytes, a repeated 32-bit word.
- **Published seeds.** Test vectors, tutorial phrases, README examples.
- **Raw keys outside BIP-39.** `privkey = 1`, small integers, hex patterns as
  WIF, single-round-SHA256 brainwallets.

### Word patterns and bit patterns are the same object

A mnemonic is a pure function of its entropy, so a pattern in the bits and a
pattern in the words are two views of one thing: all-zero entropy is
`abandon abandon ... about`, which is why that phrase is the canonical test
vector.

They differ in period. Word patterns have an 11-bit period and read as words;
byte patterns have an 8-bit period and do not — `0xAA`-repeated entropy produces
twelve unremarkable, unrelated English words. In source, byte patterns are
authored in many forms besides hex — char arrays like `['D','E','A','D']`, escape
sequences, byte-string literals — which widens the human construction surface and
means a code detector must normalize representations before matching. We expect
this class to be the most under-covered: weakest, yet least likely to look weak,
and invisible to any word-repetition heuristic.

### Collision and ownership

Distinct users can collide on the same weak key, producing an address with
several honest claimants, each able to drain the others without malice.

This is a commingled fund. The law's answer is tracing, not forfeiture: each
contributor keeps a claim to their share, and withdrawing beyond it is
conversion. No depositor owns the address; first-to-arrive confers nothing. The
protocol implements authorization, not ownership — secure key generation only
buys a reliable coincidence between who can spend and who should, and weak keys
break the coincidence, not the entitlement.

We never adjudicate any of this, because we move nothing.

## Binding principles

1. **We touch no funds.** No transaction is signed with a key we did not generate
   — no spends, sweeps, dust, on-chain warnings, or recovery-into-escrow.

2. **The tool cannot sign.** It links no transaction-signing capability; it
   derives keys and addresses and queries balances. The code is open for audit and
   we invite it — transparency is the safeguard, not a claim that a fork could not
   add signing.

3. **We observe only public data.** We read public chain state, access no private
   system, and respect the rate limits and terms of any API we query.

4. **We retain the minimum.** Findings are
   `(pattern_id, derivation_path, address, ever_funded, first_seen, swept_after)`.
   We store no mnemonics, seeds, or extended private keys; the pattern ID
   regenerates them, so storing them only creates a target.

5. **We never ask for a seed phrase.** Any lookup we run takes an address. A field
   inviting a mnemonic is indistinguishable from a drainer.

6. **Defense ships before disclosure.** The library and wallet fixes need no
   knowledge of which addresses are funded. We land those first, on aggregate
   statistics, and decide separately what specifics are published.

7. **Evidence precedes publication.** Whether funded addresses were swept in
   seconds or sat untouched for years determines who publication endangers. We
   measure it before choosing a disclosure posture.

8. **We never assert attribution.** We can say an address is derivable from a
   low-entropy mnemonic and name the pattern class; we cannot say whose it is, and
   will not imply it to anyone. Collisions can make the claim false, and a wrong
   attribution is the one way a read-only project causes harm. Notification does
   not need ownership: warn everyone who has touched the address — over-warning
   costs nothing, under-warning is the only failure.

## What we measure

Generate mnemonics from weak patterns; derive addresses across the common paths
(BIP-44/49/84/86 and Ethereum `m/44'/60'/0'/0/i`) with an account and index
fan-out; query public chain state read-only; record whether each address has
*ever* held value.

Present balance is the wrong signal — these addresses are swept within seconds,
so a balance query alone would suggest no vulnerability exists. The metrics that
matter:

- **Sweep latency** — time from funding to drain. The headline evidence of harm.
- **Distinct funding sources per address** — on-chain evidence of key collision,
  observable without any attribution claim, and apparently unmeasured.
- **Repeated drains per pattern family** — Vasek et al. used this to infer which
  wordlists attackers already use. Applied to our families, it turns "attackers
  have surely done this" into a measurement of which patterns are saturated and
  which are unswept — the input the disclosure decision needs.

## Prior art

Vasek, Bonneau, Castellucci, Keith and Moore measured our headline metric for
*brainwallets* in "The Bitcoin Brain Drain" (FC 2016): 884 wallets over
2011-2015, ~$100K, all but 21 drained (median 21 minutes), about a dozen drainers
competing on fees. Castellucci's DEF CON 23 talk is adjacent; Milk Sad covers the
weak-RNG case at scale.

The gap is the same measurement for **BIP-39 mnemonic-pattern keys** — a later
era, a different derivation scheme, a different user. The methodology is borrowed;
the population is new.

## Primary deliverable

Two detectors sharing one core (regex prefilter, then checksum validation) with
opposite policies, because they act in opposite contexts:

- **Version-control exposure** (GitHub secret scanning): fire on *any*
  checksum-valid mnemonic. A leaked seed is a leaked secret regardless of
  strength; a strong one is more urgent, since it guards a wallet in use. The
  checksum is the discriminator against prose.
- **Wallet generation/import**: block only *predictable* mnemonics — blocking all
  is absurd when using one is the point, so entropy is the signal.

The wallet-side check is a strength advisory in the major BIP-39 libraries plus a
warning at import — a non-blocking score, not a rejection. A false positive that
blocks recovery is worse than a weak seed, which is why maintainers have
reasonably declined such checks for a decade. It must operate on the entropy, not
the word list: a repeated-word heuristic misses byte-period patterns, which
produce twelve ordinary words. Compressibility of the entropy is the right signal.

GitHub secret scanning is the second home; its regex-then-Go-validator
architecture is exactly the shape a checksum detector needs, and it already
notifies committers. There, entropy serves one purpose — denoising, not
escalation: recognizing the known-weak and test-vector set (e.g.
`abandon ... about`) suppresses the thousands of legitimate test-file matches. The
signal is asymmetric — low entropy marks a phrase as constructed, high entropy
clears nothing — so entropy suppresses known junk while the checksum confirms the
leak.

The measurement is what makes these mergeable: the theory has always been
available; a measurement of real losses with real timings for this population has
not.

## Disclosure posture

Unresolved pending data. Tiers under consideration, descending by expected impact:

1. Library and wallet fixes — no offensive detail.
2. A self-service lookup keyed on address, using k-anonymity range queries so we
   learn nothing about who checks and no target list is enumerable.
3. Routing specific findings through parties who can identify owners — exchange
   security/compliance, wallet vendors, CERTs, law enforcement.
4. Publication of the vulnerability class with aggregate statistics, no keys.

Untouched funds are what publication could endanger; for those we publish the
class without the recipe and route through intermediaries with a delay. Each
finding runs from "this category is weak" to "here is the key," and we choose
where to stop per finding.

## Open questions

- What fraction of funded weak addresses were swept immediately versus never?
- How many distinct drainers work the BIP-39 pattern space, and do they race?
- Can we detect collisions on-chain via multiple unrelated funding sources?
- Which families beyond repetition and sequence are worth enumerating, and how do
  we bound the stride space?
- What k-anonymity bucket size for the lookup?
- Who reviews this legally before publication?
