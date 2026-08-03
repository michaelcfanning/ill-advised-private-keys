using NBitcoin;
using WeakKeyScanner;

// Milestone 1: positive-control test.
//
// The BIP-39 zero-entropy vector ("abandon" x11 + "about") is the all-zeros
// member of the repeated-word family and the canonical test vector. Its
// addresses have long been public, so they must show on-chain history. A
// positive (ever-funded) result confirms the derivation + funded-check pipeline;
// a negative result means the pipeline is broken, not that the class is safe.

const string PatternType = "repeated-word (control: zero-entropy vector)";
const string AbandonVector =
    "abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon abandon about";

var outPath = Path.Combine(AppContext.BaseDirectory, "out", "control.findings.jsonl");

Console.WriteLine("ill-advised-private-keys — weak key scanner");
Console.WriteLine("Positive-control test: BIP-39 zero-entropy vector");
Console.WriteLine(new string('=', 72));
Console.WriteLine($"weak key: {AbandonVector}");
Console.WriteLine();

using var oracle = new EsploraClient();
using var findings = new FindingsWriter(outPath);
int scanned = 0, funded = 0;

foreach (var d in Bip39Deriver.Derive(AbandonVector, Network.Main, count: 1))
{
    scanned++;
    FundingInfo f;
    try
    {
        f = await oracle.CheckAsync(d.Address);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"[ERROR ] {d.Kind}: {ex.Message}\n");
        continue;
    }

    var tag = f.EverFunded ? (f.BalanceSats > 0 ? "[LIVE  ]" : "[FUNDED]") : "[clean ]";
    Console.WriteLine($"{tag} {d.Kind}");
    Console.WriteLine($"    weak key : {AbandonVector}");
    Console.WriteLine($"    address  : {d.Address}");
    Console.WriteLine($"    path     : {d.Path}");
    Console.WriteLine($"    activity : {Format.Activity(f)}");
    Console.WriteLine($"    explorer : {Explorers.Address(d.Address)}");
    Console.WriteLine();

    if (f.EverFunded)
    {
        funded++;
        findings.Write(new Finding(
            PatternType, AbandonVector, d.Kind, d.Path, "bitcoin", d.Address,
            f.EverFunded, f.TxCount, f.TotalReceivedSats, f.BalanceSats,
            Explorers.Address(d.Address)));
    }

    await Task.Delay(400); // be polite to the public API
}

Console.WriteLine(new string('=', 72));
Console.WriteLine($"scanned {scanned} addresses · {funded} funded");
Console.WriteLine($"findings written to: {findings.Path}");
Console.WriteLine();
Console.WriteLine(funded > 0
    ? "RESULT: positive signal — derivation + funded-check pipeline confirmed."
    : "RESULT: no signal — investigate the pipeline (derivation or oracle).");
return funded > 0 ? 0 : 1;
