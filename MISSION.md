# Mission

This project measures a known but undocumented class of cryptocurrency wallet
compromise: private keys derived from mnemonics that a human constructed to be
memorable rather than random. We enumerate those mnemonics, check whether the
resulting addresses have ever held value, and publish what we learn in a form
that helps wallet software prevent the next occurrence.

We do not take anything. Not a satoshi, not a wei, not as a warning, not as a
demonstration, not into protective custody. That constraint is not a policy we
intend to follow carefully; it is a property we intend to make structurally
impossible to violate.

## The vulnerability

BIP-39 encodes a mnemonic as entropy plus a checksum. The checksum constrains
which final words are legal, which is what makes a hand-built phrase feel
technically legitimate to the person building it. It validates *form*, never
*randomness*.

A user who wants a memorable seed can therefore repeat a single word and search
for a valid final word. The resulting phrase passes every check any wallet
performs. It is also trivially enumerable:

- **12 words:** 11 repeated words fix 121 of 128 entropy bits, leaving 7 free.
  128 valid final words per prefix, so 2048 x 128 = 262,144 phrases.
- **24 words:** 23 words fix 253 of 256 bits, leaving 3 free.
  8 valid final words per prefix, so 2048 x 8 = 16,384 phrases.

Under 280,000 candidates for the simplest pattern. Seconds on a laptop.

The cost of attacking this scales with the size of the weak key space, not with
the number of victims. An attacker derives each candidate key forward to an
address and tests it against the public set of funded addresses. One pass covers
every user who ever made the mistake, everywhere, simultaneously. This is why we
assume the space is already farmed rather than assuming we are first.

### What actually makes a key weak

Not obviousness in the abstract. A key is weak if it is reachable by an
enumeration that somebody would plausibly *run*. That is an empirical property
of attacker behavior, not a mathematical property of the key. The operative
definition of "weak" is "in somebody's dictionary."

Independent users reaching independently for a plausible-but-memorable value
land on the same small set, which is the same reason password dictionaries work.
So the patterns worth enumerating are the ones *people* converge on, not the ones
that are mathematically elegant.

### The user memorizes a procedure, not a phrase

This is the generalization that organizes everything else. Someone building a
memorable seed is not memorizing 12 words; they are memorizing a short rule that
regenerates the 12 words on demand. "Repeat 'abandon'." "Start at word 400 and
count up." "Every other bit set." The phrase is the output; the rule is what
lives in the user's head.

So the search space is not a list of phrases. It is a set of *short programs*
drawn from an instruction set a human would find memorable, crossed with each
program's small parameter space. The real entropy of such a key is the length of
the shortest description its author would actually use, which is typically a
dozen bits or so regardless of whether the mnemonic nominally encodes 128 or 256.

Candidate program families:

- **Repetition.** Repeat word *n*. 2048 parameters.
- **Sequence.** Start at word *n*, walk the wordlist. 2048 parameters.
- **Stride.** Start at *n*, step by *k*. ~4.2M parameters before checksum.
- **Index arithmetic.** Powers of two, primes, Fibonacci, round numbers.
- **Bit patterns on the entropy.** Alternating bits, 8-on/8-off, repeating bytes,
  ascending byte values, a repeated 32-bit word.
- **Published seeds.** Test vectors, tutorial phrases, README examples.
- **Raw keys outside BIP-39.** `privkey = 1`, small integers, hex patterns
  imported directly as WIF, single-round-SHA256 brainwallets.

These are specified concretely, with sizing and cost estimates, in
[PATTERNS.md](PATTERNS.md).

### Word patterns and bit patterns are the same object

A BIP-39 mnemonic is a pure function of its entropy, so a pattern in the bits and
a pattern in the words are two views of one thing. All-zero entropy *is*
`abandon abandon ... about`, which is exactly why that phrase became the
canonical test vector.

The two views differ in period, and the difference matters enormously. Word-level
patterns have an 11-bit period and are conspicuous when read as words. Byte-level
patterns have an 8-bit period and are conspicuous only in hex — read as a
mnemonic, a repeating-byte entropy produces twelve unremarkable, unrelated
English words.

That asymmetry defines the class we expect to be most under-covered. A user who
sets entropy to `0xAA` repeated gets a phrase that looks perfectly random, would
trip no word-repetition heuristic, and may leave its author feeling clever. It is
simultaneously the weakest kind of key and the least likely to look like one.

### Collision and ownership

Distinct users can collide on the *same* weak key. An address may have several
honest claimants, each believing it is solely theirs, either capable of draining
the other with no malice involved.

Mixing does not extinguish anyone's claim. This is an ordinary commingled-fund
problem, and the law's answer is tracing, not forfeiture: each contributor
retains a claim to their share, and withdrawing beyond it is conversion. No
depositor "owns the address," and first-to-arrive confers nothing.

The protocol is silent on all of this. It implements authorization, never
ownership; secure key generation buys only a reliable *coincidence* between who
can spend and who should. Weak keys break the coincidence, not the entitlement.

We never have to adjudicate any of it, because we move nothing. A rule requiring
us to identify the rightful owner would be unimplementable in exactly the cases
that matter most. Ours is robust to the question staying open.

## Binding principles

**1. We never touch funds.** No transaction is ever signed with a key that was
not generated for our own use. No spends, no sweeps, no dust, no on-chain
warning messages, no recovery-into-escrow. Taking a trivial amount to signal
benign intent is still theft, and it is also self-defeating: a spend from a weak
address can be replaced in the mempool by anyone else holding the same key, at a
fee they will always outbid us on, because their upside is the whole balance and
ours is a delivered message.

**2. The constraint is architectural, not procedural.** The scanner links no
transaction-signing capability at all. It derives keys, derives addresses, and
queries balances. It cannot construct a transaction, so "we did not touch it"
is a fact about the binary rather than a promise about our conduct.

**3. We observe only public data.** Blockchain state is public. We read it. We
access no private system, exploit no service, and bypass no control. We respect
the rate limits and terms of any API we query.

**4. We retain as little as possible.** Findings are recorded as
`(pattern_id, derivation_path, address, ever_funded, first_seen, swept_after)`.
We do not persist mnemonics, seeds, or extended private keys. The pattern ID
regenerates them deterministically, so storing them buys nothing and creates a
target worth stealing.

**5. We never ask anyone for a seed phrase.** Any lookup we operate accepts an
address. A field that invites users to paste a mnemonic is indistinguishable
from a drainer and trains exactly the habit that gets people robbed.

**6. Defense ships before disclosure.** The protective fix requires no offensive
detail. A strength advisory in mnemonic libraries and a warning at the wallet
import step need to know nothing about which addresses are funded. We land those
first, on the strength of aggregate statistics, and decide separately how much
specificity is ever published.

**7. Evidence precedes publication.** We do not yet know whether the funded
addresses we find were swept in seconds or have sat untouched for years. That
measurement determines who is endangered by publishing and who is already
long compromised. We take it before we commit to a disclosure posture.

**8. We never assert attribution.** We can say that an address is derivable from
a low-entropy mnemonic and name the pattern class. We cannot say whose address it
is, and we will not imply it to an exchange, a CERT, or anyone else. We lack the
evidence, collisions make the claim potentially false, and a wrong attribution is
the one way a read-only project could still cause real harm.

Notification does not require resolving ownership, because warning is not
zero-sum. The question is never "who owns this" but "who should be told," and the
answer is everyone who has touched the address. Over-warning costs nothing;
under-warning is the only failure mode.

## What we do

Generate mnemonics from human-plausible weak patterns; derive addresses across
the common paths (BIP-44/49/84/86 and Ethereum's `m/44'/60'/0'/0/i`) with an
account and index fan-out; query public chain state read-only; record whether
each address has *ever* held value.

Present balance is the wrong signal. These addresses are swept within seconds of
receiving funds, so a balance query alone would suggest the vulnerability does
not exist. Transaction history is the evidence of harm, and the distribution of
sweep latencies is the measurement that matters.

We should also record the number of *distinct funding sources* per weak address.
Multiple unrelated depositors into one weak address is on-chain evidence of key
collision, observable by us without any attribution claim, and as far as we can
tell nobody has measured it.

And we should count **repeated drains per pattern family**. Vasek et al. used
this to estimate which wordlists attackers were already using: an address drained
by several distinct parties indicates several attackers cover that source. Applied
to our pattern families, it converts "surely attackers have done this already"
from an assumption into a measurement, and it tells us directly which patterns are
already saturated and which are genuinely unswept — which is exactly the input the
disclosure decision needs.

## Prior art

This ground is partly covered, and we should not claim otherwise.

Vasek, Bonneau, Castellucci, Keith and Moore measured exactly our headline metric
for *brainwallets* in "The Bitcoin Brain Drain" (FC 2016): 884 wallets over
2011-2015, roughly $100K, all but 21 drained, usually inside 24 hours and often
within minutes or seconds, with about a dozen distinct drainers competing and
paying high fees to win the race. Castellucci's DEF CON 23 talk and Milk Sad's
update #16 cover adjacent territory. Milk Sad's own disclosure covers the
weak-RNG case at scale.

What is missing is the same measurement for **BIP-39 mnemonic-pattern keys**,
which are a distinct population: a later era, a different derivation scheme, and
a different kind of user. The methodology is borrowed; the population is new.

## What we never do

Sign a transaction with a key belonging to someone else. Move funds for any
reason including their protection. Send an on-chain message, dust, or "penny
warning" to a vulnerable address. Publish a private key, seed, or mnemonic.
Import discovered keys into any wallet capable of broadcasting. Solicit seed
phrases from anyone. Retain key material we do not need.

## Primary deliverable

Two detectors sharing one core but applying opposite policies, because they act in
opposite contexts (developed in full below):

- **Version-control exposure** (GitHub secret scanning): fire on *any*
  checksum-valid mnemonic. A leaked seed is a leaked seed regardless of strength.
- **Wallet generation/import**: block only the *predictable* mnemonics. Blocking
  all of them is absurd, so entropy is the signal.

For the wallet-side check specifically: a mnemonic strength advisory in the major
BIP-39 implementations and a warning at the import step. Deliberately *not* a
rejection: a false positive would leave a user unable to recover their own funds,
which is a far worse outcome than a weak seed, and it is why maintainers have
reasonably declined such checks for a decade. The correct shape is a non-blocking
score in the library and a clear warning in the UI.

That wallet-side check must operate on the **entropy**, not on the word list. A heuristic that
counts repeated words is easy to write, easy to merge, and blind to precisely the
class we expect to matter most: byte-period bit patterns produce mnemonics of
twelve ordinary unrelated words. Compressibility of the entropy, or the length of
the shortest program generating it, is the right signal.

A second home for the detection is GitHub secret scanning, whose architecture is
already a regex prefilter followed by a Go post-processing validator — the exact
shape a BIP-39 checksum detector needs. That catches mnemonics *already committed
to public repos*, complementing the library check that prevents new ones. It is
pure defense: we contribute a detector, handle no key material, and assert no
attribution, while relying on a platform that already notifies committers
automatically.

**The two homes need opposite logic, and this is the key design point.** In the
version-control exposure context, entropy is irrelevant: any checksum-valid
mnemonic in a repo is a leaked secret, and a strong one is if anything more urgent
than a weak one, because it protects a wallet someone actually relies on. So the
detector fires on *every* valid mnemonic; the checksum is the whole discriminator,
separating a real phrase from prose that happens to use wordlist words. At the
wallet generation/import step the policy inverts: blocking all mnemonics is absurd
because using one is the point, so entropy becomes the only signal — block the
predictable, allow the rest. Same detector core, opposite policy.

Entropy has exactly one role *inside* secret scanning, and it is denoising, not
escalation. The canonical `abandon abandon ... about` vector and a few tutorial
seeds appear in thousands of legitimate test files; recognizing the known-weak and
known-vector set lets the scanner suppress or down-rank them so real leaks are not
buried. The signal is asymmetric: low entropy positively marks a phrase as
constructed (a doomed real key or a test vector), but high entropy clears nothing,
since a chance prose match looks as random as a real seed. Entropy suppresses the
known-junk tail; the checksum confirms the leak.

The evidence is what makes this mergeable. Maintainers have had the theory
available the whole time; what has not existed is a measurement of real losses
with real timings attached for this population of keys.

## Disclosure posture

Deliberately unresolved pending data, and to be settled by it. The tiers under
consideration, in descending order of expected impact:

1. Library and wallet fixes, requiring no offensive detail.
2. A self-service lookup keyed on address, using k-anonymity range queries so we
   learn nothing about who is checking and no target list is enumerable.
3. Routing specific findings through parties who can identify owners: exchange
   security and compliance teams, wallet vendors, CERTs, law enforcement.
4. Publication of the vulnerability class with aggregate statistics and no keys.

Findings whose funds have sat untouched are the ones publication could actually
endanger. For those we publish the class without the recipe, and route through
intermediaries with a delay. Every finding sits on a spectrum from "this
category is weak" to "here is the key," and we choose where to stop
independently for each.

## Open questions

- What fraction of funded weak addresses were swept immediately versus never?
- How many distinct drainers are active in the BIP-39 pattern space, and do they
  race each other as the dozen brainwallet drainers did in 2016?
- Can we detect key collisions on-chain via multiple unrelated funding sources?
- Which program families beyond repetition and sequence are worth enumerating,
  and how do we bound the stride space?
- What is the right k-anonymity bucket size for the lookup service?
- Who reviews this legally before anything is published?
