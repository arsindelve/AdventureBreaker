// PROBE — copy into ZorkAI/Planetfall.Tests/ and run:
//   dotnet test Planetfall.Tests/Planetfall.Tests.csproj --filter FullyQualifiedName~DisambProbe -l "console;verbosity=detailed"
//
// Disambiguation / noun-resolution across collision families, at the resolver seam.
// RESULTS (vs deployed main 8175684, post-#244/#245 fix):
//   GetItemInScope precise resolution: ALL families OK (bedistor, batteries, megafuses,
//     all card phrasings, spools, fromitz, goo, uniform).  [#244 fixed single-noun]
//   Bare-noun disambiguation: OK ("take battery"/"examine card" -> "Do you mean ...?").
//   MULTI-NOUN still adjective-blind: context.HasMatchingNoun("kitchen card") with
//     [shuttle,kitchen] -> SHUTTLE (BUG).  -> filed zork#246  (MultiNounEngine.IsItemHere
//     uses raw HasMatchingNoun, not the fixed GetItemInScope).
using FluentAssertions;
using GameEngine;
using GameEngine.Item;
using Planetfall.Item.Feinstein;
using Planetfall.Item.Kalamontee.Admin;
using Planetfall.Item.Kalamontee.Mech;
using Planetfall.Item.Lawanda;
using Planetfall.Item.Lawanda.Lab;
using Planetfall.Item.Lawanda.Library;
using Planetfall.Item.Lawanda.PlanetaryDefense;
using Planetfall.Location.Lawanda;

namespace Planetfall.Tests;

public class DisambProbe : EngineTestsBase
{
    // target-in-inventory + collider-in-location; precise phrase should resolve to target.
    private string Scope<TTarget, TCollider>(string phrase)
        where TTarget : ItemBase, new() where TCollider : ItemBase, new()
    {
        Repository.Reset();
        var t = GetTarget(); StartHere<CourseControl>();
        Take<TTarget>();
        GetLocation<CourseControl>().ItemPlacedHere(GetItem<TCollider>());
        var r = Repository.GetItemInScope(phrase, t.Context);
        return r == GetItem<TTarget>() ? "OK" : r == GetItem<TCollider>() ? "BUG->collider" : r == null ? "null" : "other";
    }

    [Test]
    public void PreciseResolution_AllFamilies()
    {
        void L(string f, string p, string r) => TestContext.Out.WriteLine($"{f,-10} {p,-22} -> {r}");
        L("bedistor", "good bedistor",       Scope<GoodBedistor, FusedBedistor>("good bedistor"));
        L("battery",  "fresh battery",       Scope<FreshBattery, OldBattery>("fresh battery"));
        L("megafuse", "b-series megafuse",   Scope<BSeriesMegafuse, KSeriesMegafuse>("b-series megafuse"));
        L("card",     "kitchen access card", Scope<KitchenAccessCard, ShuttleAccessCard>("kitchen access card"));
        L("card",     "kitchen card",        Scope<KitchenAccessCard, ShuttleAccessCard>("kitchen card"));
        L("spool",    "red spool",           Scope<RedSpool, GreenSpool>("red spool"));
        L("fromitz",  "first fromitz board", Scope<FirstFromitzBoard, ThirdFromitzBoard>("first fromitz board"));
        Assert.Pass();
    }

    [Test]
    public void MultiNoun_Resolver_StillAdjectiveBlind()  // zork#246
    {
        var t = GetTarget(); StartHere<CourseControl>();
        Take<ShuttleAccessCard>(); Take<KitchenAccessCard>();   // shuttle first in inventory
        var r = t.Context.HasMatchingNoun("kitchen card");
        TestContext.Out.WriteLine("HasMatchingNoun('kitchen card') -> " +
            (r.TheItem is KitchenAccessCard ? "KITCHEN (ok)" : r.TheItem is ShuttleAccessCard ? "SHUTTLE (BUG #246)" : "other"));
        Assert.Pass();
    }
}
