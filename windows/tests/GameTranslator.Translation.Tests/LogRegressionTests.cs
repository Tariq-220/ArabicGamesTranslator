using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class LogRegressionTests {
 [Theory][InlineData("192.168.137.1",true)][InlineData("192.168.43.1",true)][InlineData("172.20.10.4",false)][InlineData("8.8.8.8",false)]
 public void WindowsHotspotHostIsRecognizedWithoutTreatingCellularAsAHotspot(string ip,bool expected)=>Assert.Equal(expected,GameTranslator.Core.LgTv.LocalNetwork.IsHotspotHost(System.Net.IPAddress.Parse(ip)));
 [Theory]
 [InlineData("by the end of the dock", "بحلول نهاية •Nit's the end of the•inonth.")]
 [InlineData("Come on!", "ekhi hi hit. هيا!")]
 [InlineData("You big dozy lizard.", "أنت سحلية dozy كبيرة.")]
 [InlineData("Y See ac", "Y انظر ac")]
 [InlineData("Hello.", "Hello")]
 [InlineData("Hmm", "HTML")]
 public void MixedOrUntranslatedProseIsRejected(string source,string arabic)=>Assert.True(ArabicOutputGuard.Reject(source,arabic));
 [Theory]
 [InlineData("Jason: Let's go.", "Jason: هيا بنا.")]
 [InlineData("Call D-Man.", "اتصل بـ D-Man.")]
 [InlineData("Open your iPhone.", "افتح iPhone.")]
 [InlineData("GTA VI", "GTA VI")]
 [InlineData("Follow Lucia", "اتبع لوسيا.")]
 public void NamesAndArabicArePreserved(string source,string arabic)=>Assert.False(ArabicOutputGuard.Reject(source,arabic));
 [Theory][InlineData("Rescue Donnie again")][InlineData("Hold off the Fnal Chapter")][InlineData("Help build the coffee table")][InlineData("Pick up the glass top")]
 public void NewPhoneObjectivesDoNotHideDialogue(string task){Assert.Equal("Get down!",DialogueProfile.DialogueOnly(Reading.FromText("Get down!\n"+task),new(0,0,1,1),"GTA6").Text);}
 [Fact]public void PersistentObjectiveCannotHideDialogueAboveIt(){var r=new Reading([new("You're not going anywhere.",new(.15,.1,.85,.2)),new("Follow Lucia",new(.4,.7,.6,.8))]);var f=DialogueProfile.DialogueOnly(r,new(0,.7,1,.97),"GTA6").SubtitleBlock(new(0,.7,1,.97));Assert.Equal("You're not going anywhere.",f.Text);}
 [Fact]public void SingleObjectiveAndSpokenFollowMeSurvive(){Assert.Equal("Follow Lucia",DialogueProfile.DialogueOnly(Reading.FromText("Follow Lucia"),new(0,0,1,1),"GTA6").Text);Assert.Equal("Follow Lucia.\nYes.",DialogueProfile.DialogueOnly(Reading.FromText("Follow Lucia.\nYes."),new(0,0,1,1),"GTA6").Text);}
}
