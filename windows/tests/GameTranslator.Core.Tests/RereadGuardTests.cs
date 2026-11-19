using Xunit;
namespace GameTranslator.Core.Tests;
public class RereadGuardTests {
 [Fact]public void DirtyPunctuatedOcrWaitsForConfirmationButCleanDialogueDoesNot(){
  var dirty=new SubtitleStabilizer{CleanFastPathOnly=true};Assert.Equal(StabilizerKind.None,dirty.Feed("Once I get this smtcß.box off my ankle.").Kind);
  var clean=new SubtitleStabilizer{CleanFastPathOnly=true};Assert.Equal(StabilizerKind.Translate,clean.Feed("Get down!").Kind);
 }
 [Theory][InlineData("Hey, have fun.","Hey, have f")][InlineData("Okay, that sounds good.","Okay, that sounds")]
 public void LogV29TruncatedLastWordDoesNotReplaceFullCaption(string full,string partial){var g=new RereadGuard();g.Filter(full,0);Assert.Equal(full,g.Filter(partial,700));}
 [Theory][InlineData("secure?","You call this place secure?")][InlineData("the door.","Please close the door.")][InlineData("We should leave", "We should leave right now.")]
 public void AReadableFragmentCannotBeatItsCompleteReading(string shortText,string fullText)=>Assert.Equal(fullText,OcrPassSelection.Choose(Reading.FromText(shortText),Reading.FromText(fullText)).Text);
 [Fact]public void DifferentMeaningIsNotMerged()=>Assert.Equal("We can go.",OcrPassSelection.Choose(Reading.FromText("We can go."),Reading.FromText("We cannot go.")).Text);
 [Fact]public void OriginalReadablePixelsBeatLongerMaskGarbage(){var clean=Reading.FromText("Okay, see? I told ya.");Assert.Equal(clean.Text,OcrPassSelection.Choose(clean,Reading.FromText("Okay. see? Iloid ya. some additional invented words")).Text);}
 [Fact]public void EmptyOriginalStillUsesMask()=>Assert.Equal("Get down!",OcrPassSelection.Choose(Reading.Empty,Reading.FromText("Get down!")).Text);
 [Fact]public void JoinedWordsDoNotReplaceRecentCleanSentence(){var guard=new RereadGuard();var good="Well, it's a nice fresh day for flying, sir.";Assert.Equal(good,guard.Filter(good,0));Assert.Equal(good,guard.Filter("Well, it's anicefreshdayfornying,sir.",80));}
 [Fact]public void TruncatedObjectiveKeepsTheCompleteReading(){var guard=new RereadGuard();Assert.Equal("Mount your horse",guard.Filter("Mount your horse",0));Assert.Equal("Mount your horse",guard.Filter("Mount your",100));}
 [Theory][InlineData("We won.","We lost.")][InlineData("He's alive.","He's dying.")][InlineData("I can do it.","I can't do it.")]
 public void RealMeaningChangesAreNeverFuzzyMerged(string before,string after){var guard=new RereadGuard();guard.Filter(before,0);Assert.Equal(after,guard.Filter(after,80));}
 [Fact]public void ASeparateShortUtteranceStillPasses(){var guard=new RereadGuard();guard.Filter("You can see the house.",0);Assert.Equal("You can!",guard.Filter("You can!",50));}
 [Fact]public void ClearAndExpirationDoNotRestoreOldScene(){var guard=new RereadGuard();guard.Filter("Mount your horse",0);guard.Filter("",1000);Assert.Equal("Mount your",guard.Filter("Mount your",1100));guard.Reset();Assert.Equal("Mount your",guard.Filter("Mount your",1200));}
}
