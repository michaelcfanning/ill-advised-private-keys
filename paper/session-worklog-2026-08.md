# Session worklog — ever-funded measurement + analysis (2026-08-12 → 08-18)

A curated record of the work and, importantly, how the interpretation evolved. The
back-and-forth corrections are kept deliberately: they are why the final numbers are
conservative.

## Goal

Extend the passive weak-key measurement from the mnemonic-pattern space to the full
ever-funded chain, add the raw-key (target-K) families, and build the analysis needed to
say what the data *means* without overstating it.

## What was built

- **Generators**: raw target-K F1 (periodic bit-fills, unit width ≤ 24) and F2 (hex-word +
  single-byte fills); `cycle:2` mnemonic pattern; parallelized `emit --raw` and
  `emit --brain` (36-core; 64M-password brainwallet emit ~25 min).
- **nodewalk**: ever-funded + sweep index from a local fully-synced bitcoind, streaming
  blocks in height order with a parallel prefetch pipeline; funder resolution (input-address
  lookup on funding txs) with `--no-funders` for heavily-funded sets; resumable via JSON
  checkpoint.
- **analyze**: the interpretation layer —
  - raced-vs-custody disposition (a drain is a fee-race → latency ≈ 0; any latency ⇒ owner);
  - sweeper-reach (a serial collector draining ≥2 weak addrs = a bot);
  - four-actor taxonomy: researchers (honeypot) / general users (custody) / larkers (famous
    weak string) / bad guys (sweepers);
  - general seeding/honeypot detection (large cluster, identical small amount, one month);
  - USD-at-time valuation (deposit-date price — money of the day);
  - loss-concentration view (top-N per password, cumulative %, Gini).

## Walks

| Walk | Scope | Result |
| --- | --- | --- |
| Combined (mnemonic repeat/forward/backward + target-K F1/F2) | full chain [0, 962,187] | 195 funded weak addrs, 2 live (dust) |
| Brainwallet (CrackStation 127.9M addrs, funderless) | full chain [0, 962,927] | 18,592 funded addrs, **0 live** |

## Findings (final, honest)

- **Mnemonic + target-K**: good-faith-victim loss **≤ 1.03 BTC ≈ $16k in money-of-the-day**
  (68 addrs, 61 keys) — an upper bound. Most funded *value* is **self-custody**, dominated by
  the `0xFACED`-repeat key (29.79 BTC, owner-moved, ≈ $190k when it moved in 2018). Value is
  extreme-concentrated (top-5 keys = 96.6%, Gini 0.99).
- **Brainwallet**: replication + extension of FC16. ~1,249 meaningful passwords (≥10k sats)
  vs FC16's ~884 — same order. Genuine drains ~96 BTC raw but only ~**$42k at the time of
  theft**. 0 live. Value in ~14 ultra-guessable passwords (`asdfghjkloiuytrewq`, `deadsheep`,
  `wallet`, keyboard walks, a pangram).
- **Confirmed swept classes**: single-byte fills (`0x11`, `0xbb`), `deadbeef`,
  `repeat`/`forward` mnemonics — raced by serial-collector bots. **Apparent blind spot**: wide
  periodic fills (`0xFACED`), which appear only as self-custody.
- **0 live balances anywhere** — nothing at risk that a publication could point attackers at.

## Interpretation corrections (kept on purpose)

1. Called the brainwallet result a "headline / 20× FC16" → **walked back**: the 18k count was
   inflated by a 2013 dust-seeding campaign; the meaningful population (~1,249) ≈ FC16.
2. Called the 17k dust "custody" → **corrected**: it is a researcher **honeypot**, not custody
   and not victims (swept by bots, but conscious input, no victim).
3. Reported drains at today's BTC price → **corrected** to USD-at-time (~$42k, not ~$6M).
4. Split larker vs good-faith victim with a fame heuristic → **bounded honestly**: every
   password is dictionary-guessable, so the split is not resolvable per deposit; "good-faith
   victim" is an upper bound.

## The 2013 honeypot campaign (observed prior art)

17,108 brainwallet addresses seeded in August 2013 with an identical 5,460 sats (= 10× dust)
from 34 batched funders, never reclaimed, swept by the industrial bots. Reads as a ~0.93 BTC
(~$100-at-the-time) measurement experiment — prior art for our own honeypot proposal, and
evidence the method works.

## Coverage (what is NOT yet enumerated)

Walked: repeat/forward/backward (target-E), F1/F2 (target-K), brainwallet `SHA256(pw)`-as-key.
Not done: `stride`/`cycle:2` walks; **F7 `SHA256(pw)` as *entropy*** (PATTERNS.md's highest-value
novel family); F3 counters, F4 sparse/small-integers/single-bit, F5 ASCII payloads, F6
structured decimal, F8 nothing-up-my-sleeve constants; AI-composed memorable phrases; index
depth beyond 0. The honeypot proposal is **gated** on closing these.

## Practical bottom line

The impact is **small**. Genuine good-faith-victim loss is low four/low five figures in
money-of-the-day across everything measured; the large-looking BTC totals are self-custody or
larks, valued at today's price. The contribution is the taxonomy of newly-measured key classes
and the raced-vs-custody / four-actor method that keeps self-custody and larks out of "loss".
