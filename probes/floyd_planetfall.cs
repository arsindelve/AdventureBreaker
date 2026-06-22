// PROBE — copy into ZorkAI/Planetfall.Tests/ and run:
//   dotnet test Planetfall.Tests/Planetfall.Tests.csproj --filter FullyQualifiedName~FloydProbe -l "console;verbosity=detailed"
//
// Floyd companion — deterministic seams only (power/scoring/take/give/search).
// The AI chatter (PerformRandomAction, OnBeingTalkedTo) and random card-offer are dice/AI-gated
// and suppressed in tests, so they're out of scope here.
//
// RESULTS (vs deployed main 8175684):
//   BUG (filed zork#254, FIXED): activation +2 re-awarded on every "activate floyd" during the
//     3-turn wake-up countdown (score 2 -> 4 -> 6). Award was guarded on HasEverBeenOn, which only
//     flips when the countdown completes. Fix: dedicated one-shot HasAwardedActivationPoints flag.
//   CLEAN (confirmed — do not re-file):
//     - normal activation pays exactly +2; deactivate/reactivate after waking pays +0.
//     - take floyd ON -> "too heavy ... drops him", NOT pocketed; take floyd OFF -> narrator, also
//       not pocketed (CannotBeTakenDescription is null when off, but base take never adds him).
//     - give the diary/key to floyd -> held; give a 2nd item while holding -> "shrugs, drops it"
//       (covered by existing FloydTests.GiveSomethingToFloyd*).
//     - search floyd OFF -> finds LowerElevatorAccessCard; search ON -> "giggles/tickle".
//   ARTIFACT (not a bug): "give card to floyd" no-ops because bare "card" doesn't resolve to
//     LowerElevatorAccessCard (multi-noun adjective resolution, zork#246 family). Use the item's
//     real noun ("diary"/"key") and give works fine.
using FluentAssertions;
using Planetfall.Item.Kalamontee.Mech.FloydPart;
using Planetfall.Location.Kalamontee.Mech;

namespace Planetfall.Tests;

public class FloydProbe : EngineTestsBase
{
    private void P(string c, string? r) => TestContext.Out.WriteLine($"  '{c}' => [{r?.Replace("\n"," ").Trim()}]");

    [Test]   // zork#254
    public async Task Activate_Spam_During_Countdown_Farms_Points()
    {
        var t = GetTarget(); StartHere<RobotShop>();
        // BEFORE FIX: score 6.  AFTER FIX: score 2.
        await t.GetResponse("activate floyd");
        await t.GetResponse("activate floyd");
        await t.GetResponse("activate floyd");
        TestContext.Out.WriteLine("  score after 3x activate-during-countdown = " + t.Context.Score + " (fixed=2, was 6)");
    }

    [Test]   // confirmed clean
    public async Task Take_Floyd_Not_Pocketable()
    {
        var t = GetTarget(); StartHere<RobotShop>();
        var f = GetItem<Floyd>(); f.IsOn = true; f.HasEverBeenOn = true; f.TurnOnCountdown = 0;
        P("take floyd (ON)", await t.GetResponse("take floyd"));
        TestContext.Out.WriteLine("  pocketed? " + t.Context.HasItem<Floyd>());   // false
    }
}
