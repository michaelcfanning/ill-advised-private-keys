using System.Security.Cryptography;
using System.Text;
using NBitcoin;

namespace WeakKeyScanner;

/// <summary>
/// The classic brainwallet derivation the FC16 study measured: private key =
/// SHA256(password), then P2PKH. Both compressed and uncompressed public keys are
/// emitted — historically funded under either, and the source of the 884-vs-845
/// discrepancy in that paper. No PBKDF2, no derivation tree: cheap per candidate.
/// </summary>
public static class BrainwalletDeriver
{
    public static IEnumerable<DerivedAddress> Derive(string password, Network network)
    {
        byte[] h = SHA256.HashData(Encoding.UTF8.GetBytes(password));

        Key compressed, uncompressed;
        try
        {
            compressed = new Key(h, -1, fCompressedIn: true);
            uncompressed = new Key(h, -1, fCompressedIn: false);
        }
        catch
        {
            yield break; // hash is 0 or >= curve order: not a valid key (astronomically rare)
        }

        yield return new DerivedAddress(
            "brainwallet p2pkh (compressed)", "sha256(pw)",
            compressed.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString());
        yield return new DerivedAddress(
            "brainwallet p2pkh (uncompressed)", "sha256(pw)",
            uncompressed.PubKey.GetAddress(ScriptPubKeyType.Legacy, network).ToString());
    }
}
