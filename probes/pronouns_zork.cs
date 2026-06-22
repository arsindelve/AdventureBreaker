// PROBE — copy into ZorkAI/UnitTests/ and run:
//   dotnet test UnitTests/UnitTests.csproj --filter FullyQualifiedName~PronounProbe -l "console;verbosity=detailed"
//
// "it" / "them" antecedent resolution. RESULTS (vs deployed main 8175684):
//   BUG (filed zork#248):
//     - "them" never resolves to a COLLECTION: take all -> drop/put/take them => "What item...?"
//     - movement clears the antecedent for CARRIED items: take lantern; east; drop it => "What item...?"
//   CLEAN (confirmed working — do not re-file):
//     - single it (take/drop/examine, most-recent tracking), them for an IPluralNoun (candles)
//     - "drop IT." (caps + trailing period) -> Dropped
//     - "it" after disambiguation: examine card -> Which? -> kitchen -> take it -> takes kitchen card
//     - "eat it" with no antecedent -> correct "What item are you referring to?"
//   NOT A BUG (harness artifact): examine <out-of-scope/consumed item> -> empty []  (AI gen suppressed in tests)
//   DEFENSIBLE (not filed): "open case" sets LastNoun="case", so "put it in case" -> "put case in case".
using NUnit.Framework;
using ZorkOne.Item;
using ZorkOne.Location;

namespace UnitTests;

public class PronounProbe : EngineTestsBase
{
    private void P(string c, string? r) => TestContext.Out.WriteLine($"  '{c}' => [{r?.Replace("\n"," ").Trim()}]");

    [Test]   // zork#248-A
    public async Task Them_ForCollection()
    {
        async Task Run(string label, params string[] cmds)
        {
            var t = GetTarget(); StartHere<LivingRoom>();
            string? last = null; foreach (var c in cmds) last = await t.GetResponse(c);
            TestContext.Out.WriteLine($"{label}: '{cmds[^1]}' => {last?.Replace("\n"," ").Trim()}");
        }
        await Run("take all/drop them ", "take all", "drop them");
        await Run("take all/put them  ", "take all", "open case", "put them in case");
        await Run("two singles        ", "take lantern", "take sword", "drop them");
    }

    [Test]   // zork#248-B
    public async Task Move_ClearsAntecedent()
    {
        var t = GetTarget(); StartHere<LivingRoom>();
        await t.GetResponse("take lantern");
        await t.GetResponse("examine it");
        await t.GetResponse("east");
        TestContext.Out.WriteLine("  LastNoun after move = '" + t.Context.LastNoun + "'");
        P("drop it", await t.GetResponse("drop it"));
    }

    [Test]   // confirmed clean
    public async Task CleanCases()
    {
        var t = GetTarget(); StartHere<LivingRoom>();
        await t.GetResponse("take sword");
        P("drop IT.", await t.GetResponse("drop IT."));      // -> Dropped
        var t2 = GetTarget(); StartHere<LivingRoom>();
        P("eat it (no antecedent)", await t2.GetResponse("eat it")); // -> What item...?
    }
}
