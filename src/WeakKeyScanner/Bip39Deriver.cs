using NBitcoin;

namespace WeakKeyScanner;

/// <summary>One address derived from a mnemonic under a specific script type/path.</summary>
public readonly record struct DerivedAddress(string Kind, string Path, string Address);

/// <summary>
/// Turns a BIP-39 mnemonic into addresses across the common Bitcoin script types.
/// Derivation only — no signing, no transaction construction.
/// </summary>
public static class Bip39Deriver
{
    // Each script type at its standard BIP purpose path: account 0, external chain.
    private static readonly (string Kind, string AccountPath, ScriptPubKeyType Type)[] Specs =
    {
        ("p2pkh (BIP44)",        "44'/0'/0'", ScriptPubKeyType.Legacy),
        ("p2sh-p2wpkh (BIP49)",  "49'/0'/0'", ScriptPubKeyType.SegwitP2SH),
        ("p2wpkh (BIP84)",       "84'/0'/0'", ScriptPubKeyType.Segwit),
        ("p2tr (BIP86)",         "86'/0'/0'", ScriptPubKeyType.TaprootBIP86),
    };

    /// <summary>
    /// Derive the external-chain addresses for indices [0, count) under every script type.
    /// </summary>
    public static IEnumerable<DerivedAddress> Derive(string mnemonic, Network network, int count = 1)
    {
        var root = new Mnemonic(mnemonic, Wordlist.English).DeriveExtKey();
        foreach (var spec in Specs)
        {
            for (int i = 0; i < count; i++)
            {
                var path = $"{spec.AccountPath}/0/{i}";
                var pub = root.Derive(new KeyPath(path)).PrivateKey.PubKey;
                var address = pub.GetAddress(spec.Type, network).ToString();
                yield return new DerivedAddress(spec.Kind, "m/" + path, address);
            }
        }
    }
}
