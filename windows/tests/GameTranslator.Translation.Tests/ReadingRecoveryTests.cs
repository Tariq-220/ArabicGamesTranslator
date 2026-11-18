using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ReadingRecoveryTests
{
 [Fact]public void RightInteractionLabelsCannotDisplaceDialogue(){
  var area=new TextBox(0,0,1,1);
  var raw=new Reading([new("Keep going. Through the palms.",new(.33,.78,.67,.82)),new("GREET X",new(.8,.9,.97,.93)),new("ROB Y",new(.8,.94,.97,.97))]);
  var picked=DialogueProfile.DialogueOnly(raw,area,"RDR2").WithoutButtonPrompts().WithoutOcrNoise().SubtitleBlock(area);
  Assert.Equal("Keep going. Through the palms.",picked.Text);
 }
 [Theory][InlineData("Greet")][InlineData("Antagonize B")][InlineData("Rob Y")]
 public void PromptsAloneProduceNoDialogue(string text)=>Assert.Empty(DialogueProfile.DialogueOnly(new Reading([new(text,new(.8,.9,.98,.98))]),new(0,0,1,1),"RDR2").Lines);
 [Fact]public void ActualDialogueAndOtherGamesArePreserved(){
  var center=new TextLine("Rob!",new(.45,.8,.55,.85));var sentence=new TextLine("Don't rob him.",new(.72,.8,.95,.85));
  Assert.Equal(2,DialogueProfile.DialogueOnly(new Reading([center,sentence]),new(0,0,1,1),"RDR2").Lines.Count);
  Assert.Single(DialogueProfile.DialogueOnly(new Reading([new("Rob",new(.8,.9,.95,.95))]),new(0,0,1,1),"Other game").Lines);
 }
 [Fact]public void PromptFilteringUsesFullFrameCoordinatesOfTheCrop(){
  var crop=new TextBox(.12,.62,.98,.98);var read=new Reading([new("Antagonize",new(.85,.4,.98,.5))]);
  Assert.Empty(DialogueProfile.DialogueOnly(read,crop,"RDR2").Lines);
 }
 [Fact]public void RejectsObservedCorruptSourceWithoutRejectingNormalEnglish(){Assert.Equal("",DialogueProfile.DialogueSource("'-42.1Jl I-iA si )Ä-il","Grand Theft Auto VI"));Assert.Equal("I have 20 dollars.",DialogueProfile.DialogueSource("I have 20 dollars.","Grand Theft Auto VI"));}
 [Fact]public void DialogueRequiresBriefStabilityEvenWithPunctuation(){long time=0;var s=new SubtitleStabilizer(clock:()=>time,cleanFastPathOnly:true){MinimumStableMs=100};Assert.Equal(StabilizerAction.None,s.Feed("Hey!"));time=50;Assert.Equal(StabilizerAction.None,s.Feed("Hey!"));time=100;Assert.Equal(StabilizerAction.Translate("Hey!"),s.Feed("Hey!"));}
}
