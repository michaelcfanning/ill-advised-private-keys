using NBitcoin;

namespace WeakKeyScanner;

/// <summary>
/// Target K (see PATTERNS.md): a 256-bit scalar imported directly as a private key —
/// no BIP-39, no PBKDF2, no derivation tree. One EC multiply per candidate, ~three
/// orders of magnitude cheaper than the mnemonic path. A byte string is a valid key
/// only if it lies in [1, n-1]; the NBitcoin <see cref="Key"/> ctor rejects 0 and
/// values ≥ the curve order (so e.g. all-0xFF is valid entropy but NOT a valid key).
///
/// Emits the address under each script type a wallet would show for a raw-key import:
/// legacy P2PKH (both pubkey compressions, historically funded under either), plus
/// compressed P2WPKH and BIP86 P2TR.
/// </summary>
public static class RawKeyDeriver
{
    public static IEnumerable<DerivedAddress> Derive(byte[] key32, Network network)
    {
        Key compressed, uncompressed;
        try
        {
            compressed = new Key(key32, -1, fCompressedIn: true);
            uncompressed = new Key(key32, -1, fCompressedIn: false);
        }
        catch
        {
            yield break; // 0 or >= curve order: not a valid private key
        }

        yield return new DerivedAddress(
            "rawkey p2pkh (compressed)", "target-K",
            compressed.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString());
        yield return new DerivedAddress(
            "rawkey p2pkh (uncompressed)", "target-K",
            uncompressed.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString());
        yield return new DerivedAddress(
            "rawkey p2wpkh", "target-K",
            compressed.PubKey.GetAddress(ScriptPubKeyType.Segwit, network).ToString());
        yield return new DerivedAddress(
            "rawkey p2tr", "target-K",
            compressed.PubKey.GetAddress(ScriptPubKeyType.TaprootBIP86, network).ToString());
    }
}
