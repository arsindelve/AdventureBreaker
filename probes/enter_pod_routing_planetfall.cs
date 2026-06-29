using FluentAssertions;
using GameEngine;
using GameEngine.IntentEngine;
using Model.Intent;
using Model.Location;
using Moq;
using Model.AIGeneration;
using Planetfall.Item.Feinstein;
using Planetfall.Location.Feinstein;

namespace Planetfall.Tests;

public class TempEnterPod : EngineTestsBase
{
    [Test]
    public async Task EnterPod_RoutesToCantEnterMock_InsteadOfBulkheadClosed()
    {
        var target = GetTarget(); StartHere<DeckNine>();
        var ctx = target.Context;

        // 1) "pod" IS a valid noun -> resolves to the BulkheadDoor in scope.
        var resolved = Repository.GetItemInScope("pod", ctx);
        TestContext.Out.WriteLine("GetItemInScope('pod') -> " + (resolved?.GetType().Name ?? "null"));
        TestContext.Out.WriteLine("is ISubLocation? " + (resolved is Model.Location.ISubLocation));

        // 2) Drive EnterSubLocationEngine directly with the intent the AI produces for "enter pod".
        var gen = new Mock<IGenerationClient>();
        gen.SetupGet(g => g.IsDisabled).Returns(true); // deterministic: returns "You cannot go that way."
        var engine = new EnterSubLocationEngine();
        var (_, msg) = await engine.Process(new EnterSubLocationIntent { Noun = "pod" }, ctx, gen.Object);
        TestContext.Out.WriteLine("EnterSubLocationEngine('pod') -> " + msg);
        TestContext.Out.WriteLine("BulkheadDoor.IsOpen = " + Repository.GetItem<BulkheadDoor>().IsOpen);
    }
}
