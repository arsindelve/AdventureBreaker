// PROBE — copy into ZorkAI/Planetfall.Tests/ and run:
//   dotnet test Planetfall.Tests/Planetfall.Tests.csproj --filter FullyQualifiedName~ConferenceDialProbe -l "console;verbosity=detailed"
//
// Deep probe of the conference-room combination-lock dial/door
// (Planetfall/Item/Kalamontee/ConferenceRoomDoor.cs). Random UnlockCode = RollDice(999),
// discoverable in-game via PieceOfPaper. Set with "set/turn dial to N" while in RecArea.
//
// RESULTS (vs deployed main 8175684): DIAL/DOOR IS CLEAN — robust, no crashes.
//   Boundaries all handled gracefully:
//     1000 -> "does not go that high"; 999/0 -> set; -5 -> "do not go below zero";
//     "abc" / "1,000" / int-overflow ("2147483648", "99999999999999999999") -> "can only be set to numbers";
//     "" (no number) -> "You must specify a number"; trailing junk "50 50" -> "can only be set to numbers".
//   Word numbers parse: "twelve" -> 12, "five hundred" -> 500. "462.5" -> 462 (truncates).
//   Correct code -> "The door swings open, and the dial resets to 0." IsOpen=true, Code reset to "0".
//   examine dial -> "The dial is set to {Code}." (reflects current setting; only in RecArea).
//   open door (at RecArea) -> proper locked message (CannotBeOpenedDescription).
//   From the ConferenceRoom side (not RecArea): dial commands fall through (empty/narrator) — correct,
//     the dial is only reachable from RecArea.
//
//   SOFT SPOTS (documented, NOT filed — not player-impacting):
//     * "set door to N" / "set lock to N" -> empty (narrator). The multi-noun handler lists
//       door/lock/conference-room-door as nounOne, but a bare number isn't tokenized as a noun,
//       so no MultiNounIntent forms; only the canonical "set dial to N" (simple path) fires.
//       This is a general parser limitation (numbers aren't nouns), not door-specific logic.
//     * HasEverBeenOpened stays false after a dial-open (the flag is set only by the generic
//       "open" verb processor; the dial sets IsOpen directly). Harmless — nothing reads it for
//       this door.
using Planetfall.Item.Kalamontee;
using Planetfall.Location.Kalamontee;

namespace Planetfall.Tests;

public class ConferenceDialProbe : EngineTestsBase
{
    private void P(string c, string? r) => TestContext.Out.WriteLine($"  '{c}' => [{r?.Replace("\n"," ").Trim()}]");

    [Test]
    public async Task Dial_Edges()
    {
        var t = GetTarget(); StartHere<RecArea>();
        P("examine dial", await t.GetResponse("examine dial"));
        P("set dial to 1000", await t.GetResponse("set dial to 1000"));
        P("set dial to 999", await t.GetResponse("set dial to 999"));
        P("set dial to 0", await t.GetResponse("set dial to 0"));
        P("set dial to twelve", await t.GetResponse("set dial to twelve"));
        P("set dial to abc", await t.GetResponse("set dial to abc"));
        P("set dial to -5", await t.GetResponse("set dial to -5"));
        P("set dial (no number)", await t.GetResponse("set dial"));
        P("turn dial to 50", await t.GetResponse("turn dial to 50"));
        P("examine dial (after 50)", await t.GetResponse("examine dial"));
    }

    [Test]
    public async Task Dial_CorrectCode_Opens()
    {
        var t = GetTarget(); StartHere<RecArea>();
        var door = GetItem<ConferenceRoomDoor>();
        var code = door.UnlockCode;
        P($"set dial to {code} (correct)", await t.GetResponse($"set dial to {code}"));
        TestContext.Out.WriteLine("  IsOpen = " + door.IsOpen + "  Code reset = " + door.Code);
    }

    [Test]
    public async Task Dial_Overflow_Words_And_Synonyms()
    {
        var t = GetTarget(); StartHere<RecArea>();
        P("set dial to 2147483648 (int max+1)", await t.GetResponse("set dial to 2147483648"));
        P("set dial to 99999999999999999999", await t.GetResponse("set dial to 99999999999999999999"));
        P("set dial to five hundred", await t.GetResponse("set dial to five hundred"));
        P("set dial to 462.5", await t.GetResponse("set dial to 462.5"));
        P("set dial to 1,000", await t.GetResponse("set dial to 1,000"));
        P("open door (at RecArea)", await t.GetResponse("open door"));
        P("set lock to 50 (synonym -> narrator)", await t.GetResponse("set lock to 50"));
        P("turn dial to 50 50", await t.GetResponse("turn dial to 50 50"));
    }

    [Test]
    public async Task Dial_WrongLocation()
    {
        var t = GetTarget(); StartHere<ConferenceRoom>();
        P("set dial to 12 (from ConferenceRoom side)", await t.GetResponse("set dial to 12"));
        P("examine dial (from ConferenceRoom side)", await t.GetResponse("examine dial"));
    }
}
