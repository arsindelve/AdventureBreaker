// PROBE — copy into ZorkAI/Planetfall.Tests/ and run:
//   dotnet test Planetfall.Tests/Planetfall.Tests.csproj --filter FullyQualifiedName~DataSubsystemProbe -l "console;verbosity=detailed"
// Deep probe of diary / computer terminal (LibraryLobby) / spool reader (Library). RESULTS vs main 8175684:
//   BUG: terminal processes keypresses while OFF -> MenuState moves MainMenu->HistoryMenu with IsOn=false -> zork#252
//   CLEAN: diary 15-page read/press/rewind; spool reader insert/read(green=helicopter,red=disease)/
//          "already a spool"/"It doesn't fit" (non-spool); terminal key>count -> "no meaning",
//          leaf -> "lowest level", 0 -> up, "type N on the keyboard" multi-noun routes correctly.
using FluentAssertions;
using Planetfall.Item.Lawanda.Library.Computer;
using Planetfall.Location.Lawanda;
namespace Planetfall.Tests;
public class DataSubsystemProbe : EngineTestsBase
{
    [Test] public async Task Terminal_KeypressWhileOff_BUG() {
        var t = GetTarget(); StartHere<LibraryLobby>();
        var term = GetItem<ComputerTerminal>();
        var before = term.MenuState.CurrentItem.GetType().Name;
        await t.GetResponse("type 1");                                  // terminal is OFF
        TestContext.Out.WriteLine($"OFF: {before} -> {term.MenuState.CurrentItem.GetType().Name}  IsOn={term.IsOn}");
        term.MenuState.CurrentItem.Should().BeOfType<MainMenu>("typing while off must not navigate"); // RED today
    }
}
