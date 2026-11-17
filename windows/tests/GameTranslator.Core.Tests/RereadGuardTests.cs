using Xunit;
namespace GameTranslator.Core.Tests;
public class RereadGuardTests {
 [Fact]public void JoinedWordsDoNotReplaceRecentCleanSentence(){var guard=new RereadGuard();var good="Well, it's a nice fresh day for flying, sir.";Assert.Equal(good,guard.Filter(good,0));Assert.Equal(good,guard.Filter("Well, it's anicefreshdayfornying,sir.",80));}
 [Fact]public void TruncatedObjectiveKeepsTheCompleteReading(){var guard=new RereadGuard();Assert.Equal("Mount your horse",guard.Filter("Mount your horse",0));Assert.Equal("Mount your horse",guard.Filter("Mount your",100));}
 [Theory][InlineData("We won.","We lost.")][InlineData("He's alive.","He's dying.")][InlineData("I can do it.","I can't do it.")]
 public void RealMeaningChangesAreNeverFuzzyMerged(string before,string after){var guard=new RereadGuard();guard.Filter(before,0);Assert.Equal(after,guard.Filter(after,80));}
 [Fact]public void ASeparateShortUtteranceStillPasses(){var guard=new RereadGuard();guard.Filter("You can see the house.",0);Assert.Equal("You can!",guard.Filter("You can!",50));}
 [Fact]public void ClearAndExpirationDoNotRestoreOldScene(){var guard=new RereadGuard();guard.Filter("Mount your horse",0);guard.Filter("",1000);Assert.Equal("Mount your",guard.Filter("Mount your",1100));guard.Reset();Assert.Equal("Mount your",guard.Filter("Mount your",1200));}
}
