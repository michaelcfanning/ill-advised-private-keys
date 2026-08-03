# Pattern families

Enumerable candidate spaces for the scanner. Every family here is a *short
program plus a small parameter*, per the framing in [MISSION.md](MISSION.md).

## Two distinct targets

These must not be conflated. They have different validity rules, different
derivation costs, and produce entirely different addresses from the same bits.

**Target E — BIP-39 entropy.** A 128/160/192/224/256-bit string. The mnemonic is
a pure function of it, so we never search for a valid final word: we impose the
pattern on the entropy and *compute* the checksum. Any bit string of legal length
is valid entropy. Cost per candidate is PBKDF2-HMAC-SHA512 (2048 iterations) to
reach the seed, then a BIP-32 derivation per path, then an EC point
multiplication per address.

**Target K — raw private key.** A 256-bit scalar imported directly as WIF or hex,
with no BIP-39 and no derivation path. Must lie in `[1, n-1]` where

```
n = FFFFFFFF FFFFFFFF FFFFFFFF FFFFFFFE BAAEDCE6 AF48A03B BFD25E8C D0364141
```

Note the asymmetry this creates: all-`0xFF` is perfectly valid *entropy* but is
**not** a valid private key, since it exceeds `n`. Cost per candidate is one EC
point multiplication plus hashing — roughly three orders of magnitude cheaper
than target E, because there is no PBKDF2 and no derivation tree.

Run every family against both targets. The same pattern yields unrelated
addresses under each, and only one of the two has ever been systematically
scanned.

## F1 — Periodic fills

The master family. Take a unit `U` of width `w` bits, repeat it to fill the
target length, truncating the final repeat. This subsumes most hand-written
examples:

| Example | Unit | Width |
| --- | --- | --- |
| all-`A` in hex, `0xAAAA…` | `1010` | 4 |
| all-`B` in hex, `0xBBBB…` | `1011` | 4 |
| `11110000` repeated | `11110000` | 8 |
| `0x00`, `0xFF`, `0x01` fills | byte | 8 |
| `0xFEEDFEED` | `FEED` | 16 |
| alternating bits | `10` | 2 |
| run-length `1^k 0^k` | — | 2k |

Because a `0xAA` fill arises from `w` = 2, 4 and 8 alike, dedupe after
generation.

| Width | Raw count | Notes |
| --- | --- | --- |
| w ≤ 8 | 256 | trivial |
| w ≤ 16 | ~131,000 | trivial; enumerate fully under both targets |
| w ≤ 24 | ~33.5M | fine for target K; expensive for target E |
| w = 32 | 4.29e9 | dictionary only — see F2 |
| w ≥ 64 | — | dictionary only |

Non-byte-aligned periods matter and are easy to miss. A period of 3 (`111000…`)
or 5 or 6 produces bytes that never repeat on a byte boundary, so any
byte-oriented eyeball check or heuristic misses it entirely.

## F2 — Hex-word fills

Full enumeration is impossible at this width, so use a dictionary. Likely high
yield: programmers converge on the same magic constants.

```
deadbeef  cafebabe  feedface  baadf00d  deadc0de  8badf00d  decafbad
badc0ffe  feedbeef  deadfa11  defec8ed  c0ffee    facefeed  0ddba11
abadcafe  d15ea5e   b16b00b5  cafed00d
```

Generate from: hex-alphabet English words, leetspeak substitutions
(`o`→`0`, `i`/`l`→`1`, `s`→`5`, `e`→`3`, `t`→`7`, `b`→`8`, `g`→`9`), and known
magic numbers from file formats and debuggers. Then cross with repetition,
pairwise concatenation (`deadbeefcafebabe…`), and reversal.

## F3 — Counters and sequences

Ascending or descending bytes from any start with any stride (`00 01 02 03…`),
ascending nibbles (`0123456789abcdef` repeated), and counters at widths 8, 16
and 32. Small parameter space; enumerate fully.

## F4 — Sparse and dense keys

Low or high Hamming weight, most meaningful against target K:

- Single bit set: 256 candidates. Includes `privkey = 1`.
- Two bits set: 32,640. Three bits: ~2.7M.
- Small integers `1 … 2^24`.
- Complements of all the above.

The Bitcoin "puzzle" addresses live in this family and are actively hunted, so
expect saturation here. Useful as a *positive control* — if the scanner does not
light up on these, the pipeline is broken.

## F5 — ASCII payloads

A passphrase written into the byte array directly rather than hashed: repeated to
fill, zero-padded, or space-padded. Distinct from a brainwallet, which hashes.
Draw from the same wordlists the brainwallet work used.

## F6 — Structured decimal

Dates (`20240101…`), phone-shaped and ID-shaped digit strings, `12345678`
repeated, and digits of π, e and √2 in both decimal and hex expansion.

## F7 — Hashed passwords as *entropy*

**Probably the highest-value family in this document.**

Brainflayer and the FC16 study both compute `SHA256(password)` and use it as a
**private key**. Nobody appears to have computed `SHA256(password)` and used it
as **BIP-39 entropy**. Same passwords, same wordlists, completely different
addresses — the derivation diverges immediately, so a decade of brainwallet
scanning has never touched these.

The wordlists are off-the-shelf and their yield is already known: CrackStation
alone accounted for 640 of the 884 brainwallets Vasek et al. identified. Variants
worth generating: `SHA256(pw)`, `SHA256(SHA256(pw))`, `SHA256(pw)` truncated to
128 bits, and the same under Keccak-256.

## F8 — Nothing-up-my-sleeve constants

SHA-256 round constants, secp256k1 curve parameters, and other published
constants a user might reach for believing them "random enough."

## Cost model

Per candidate, the dominant term is EC point multiplication, once per address.

- **Target K:** ~1 point mult. Order 50 µs. 33M candidates is under an hour on
  one core.
- **Target E:** PBKDF2 (~20-50 µs) plus derivation and a point mult for every
  address in the fan-out. At ~200 addresses per candidate that is ~10 ms each, so
  65,000 candidates is minutes but 33M is hundreds of core-hours.

Which sets the enumeration policy: **go wide on target K, go deep on target E.**
Enumerate F1 fully to w ≤ 24 against raw keys; cut to w ≤ 16 against entropy, and
use dictionaries (F2, F5, F7) for anything wider.

## Address fan-out

Every candidate must be expanded across all of these, or hits will be missed:

- Public key encoding: compressed **and** uncompressed. This alone accounted for
  the 884-vs-845 gap in the FC16 results.
- Script types: P2PKH, P2SH-P2WPKH, P2WPKH, P2TR.
- Derivation paths (target E only): BIP-44/49/84/86, plus Ethereum
  `m/44'/60'/0'/0/i`; account and address indices `0 … k`.
- Also test the seed used *directly* as a key, which some tools do.

## Positive controls

The pipeline must find these or it is broken. All are long since drained.

- `abandon abandon … about` — the all-zero-entropy test vector.
- `privkey = 1` — address `1EHNa6Q4Jz2uvNExL497mE43ikXhwF6kZm`.
- Known brainwallet passwords from published lists.
