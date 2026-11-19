using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class LogRegressionTests {
 [Theory]
 [InlineData("by the end of the dock", "بحلول نهاية •Nit's the end of the•inonth.")]
 [InlineData("Come on!", "ekhi hi hit. هيا!")]
 [InlineData("You big dozy lizard.", "أنت سحلية dozy كبيرة.")]
 [InlineData("Y See ac", "Y انظر ac")]
 [InlineData("Hello.", "Hello")]
 public void MixedOrUntranslatedProseIsRejected(string source,string arabic)=>Assert.True(ArabicOutputGuard.Reject(source,arabic));
 [Theory]
 [InlineData("Jason: Let's go.", "Jason: هيا بنا.")]
 [InlineData("Call D-Man.", "اتصل بـ D-Man.")]
 [InlineData("Open your iPhone.", "افتح iPhone.")]
 [InlineData("GTA VI", "GTA VI")]
 [InlineData("Follow Lucia", "اتبع لوسيا.")]
 public void NamesAndArabicArePreserved(string source,string arabic)=>Assert.False(ArabicOutputGuard.Reject(source,arabic));
 [Fact]public void PersistentObjectiveCannotHideDialogueAboveIt(){var r=new Reading([new("You're not going anywhere.",new(.15,.1,.85,.2)),new("Follow Lucia",new(.4,.7,.6,.8))]);var f=DialogueProfile.DialogueOnly(r,new(0,.7,1,.97),"GTA6").SubtitleBlock(new(0,.7,1,.97));Assert.Equal("You're not going anywhere.",f.Text);}
 [Fact]public void SingleObjectiveAndSpokenFollowMeSurvive(){Assert.Equal("Follow Lucia",DialogueProfile.DialogueOnly(Reading.FromText("Follow Lucia"),new(0,0,1,1),"GTA6").Text);Assert.Equal("Follow Lucia.\nYes.",DialogueProfile.DialogueOnly(Reading.FromText("Follow Lucia.\nYes."),new(0,0,1,1),"GTA6").Text);}
}
