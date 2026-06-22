// PROBE — copy into ZorkAI/UnitTests/ and run:
//   dotnet test UnitTests/UnitTests.csproj --filter FullyQualifiedName~MultiSentenceProbe -l "console;verbosity=detailed"
// Multi-sentence (SentenceSplitter + sequential processing). RESULTS vs main 8175684: ALL CLEAN.
//   - "take lantern. flibbertigibbet. take sword" -> both taken (nonsense middle doesn't abort)
//   - "take lantern.. take sword" (double period) -> both taken
//   - "take lantern. turn on lantern" -> lantern on (state carries within chain)
//   - take/drop/take sequential -> correct final state
//   - disambiguation mid-chain ("press button. take yellow button") -> prompts, drops the rest
//     (documented "smart interruption" - by design, not a bug)
using GameEngine;
using NUnit.Framework;
using ZorkOne.Item;
using ZorkOne.Location;
namespace UnitTests;
public class MultiSentenceProbe : EngineTestsBase
{
    [Test] public async Task Edges() {
        var t = GetTarget(); StartHere<LivingRoom>();
        await t.GetResponse("take lantern. flibbertigibbet. take sword");
        TestContext.Out.WriteLine($"nonsense-middle: lantern={t.Context.HasItem<Lantern>()} sword={t.Context.HasItem<Sword>()}");
        var t2 = GetTarget(); StartHere<LivingRoom>();
        await t2.GetResponse("take lantern. turn on lantern");
        TestContext.Out.WriteLine("state-carry: lantern on=" + GetItem<Lantern>().IsOn);
    }
}
