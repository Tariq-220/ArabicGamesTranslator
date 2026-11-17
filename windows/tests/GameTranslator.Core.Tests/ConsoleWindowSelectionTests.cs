using Xunit;
namespace GameTranslator.Core.Tests;
public class ConsoleWindowSelectionTests {
 [Fact]public void PlayStationSelectsPlayerInsteadOfXboxOrGuide()=>Assert.Equal("PS Remote Play",ConsoleWindowSelection.Pick(["Xbox - Chrome","PS Remote Play | Download - Chrome","PS Remote Play"],true));
 [Fact]public void DetectedProcessTitleWinsOverHeuristic()=>Assert.Equal("Playing PS5",ConsoleWindowSelection.Pick(["PS Remote Play","Playing PS5"],true,"Playing PS5"));
 [Fact]public void ChiakiIsAccepted()=>Assert.Equal("chiaki-ng | PS5",ConsoleWindowSelection.Pick(["chiaki-ng | PS5"],true));
 [Fact]public void XboxModeDoesNotSelectPlayStation()=>Assert.Equal("Xbox Play - Chrome",ConsoleWindowSelection.Pick(["PS Remote Play","Xbox Play - Chrome"],false));
 [Fact]public void MissingPlayerRequiresUserSelection()=>Assert.Null(ConsoleWindowSelection.Pick(["PS Remote Play | Download - Chrome","Notes"],true));
}
