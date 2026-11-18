using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class ReviewedLanguageTests {
 [Theory][InlineData("GTA 6","Hurry! And haul ass, man.","اسحب المؤخرة يا رجل","أسرع يا رجل")][InlineData("RDR2","Please kill the engine now.","اقتل المحرك الآن.","أطفئ المحرك الآن.")][InlineData("GTA 6","We should head for the plane.","رئيس الطائرة","توجّه إلى الطائرة")]
 public void ReviewedIdiomsWorkInNewSentencesAndAcrossEngines(string game,string english,string before,string expected){Assert.Equal(expected,CommunityCorrections.Improve(game,english,before));}
 [Theory][InlineData("Show me the head of the plane.","رئيس الطائرة")][InlineData("Never mind.","اقتل المحرك")][InlineData("The word prehaul assuffix is different.","اسحب المؤخرة")][InlineData("Please kill the engine.","أطفئ المحرك.")]
 public void DoesNotRewriteUnrelatedOrAlreadyCorrectOutput(string english,string arabic){Assert.Equal(arabic,CommunityCorrections.Improve("GTA 6",english,arabic));}
 [Fact]public void UnreviewedRulesCannotBeLoaded(){var json=JsonSerializer.Serialize(new{rules=new[]{new{id="bad",game="*",source_phrase="haul ass",bad_arabic="اسحب المؤخرة",replacement="أسرع",reviewed=false}}});Assert.Throws<InvalidDataException>(()=>ReviewedLanguage.Parse(json));}
 [Fact]public void RetrievedMeaningHintsAreRelevantAndReuseTheSameCloudRequest(){Assert.Contains("hurry up",AiRequest.UserMessage(["Hurry! Haul ass!"],[],"GTA6"));Assert.DoesNotContain("hurry up",AiRequest.UserMessage(["Stay here."],[],"GTA6"));}
}
