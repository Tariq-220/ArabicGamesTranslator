using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class ReviewedLanguageTests {
 [Theory][InlineData("GTA 6","Hurry! And haul ass, man.","اسحب المؤخرة يا رجل","أسرع يا رجل")][InlineData("RDR2","Please kill the engine now.","اقتل المحرك الآن.","أطفئ المحرك الآن.")][InlineData("GTA 6","We should head for the plane.","رئيس الطائرة","توجّه إلى الطائرة")]
 public void ReviewedIdiomsWorkInNewSentencesAndAcrossEngines(string game,string english,string before,string expected){Assert.Equal(expected,CommunityCorrections.Improve(game,english,before));}
 [Theory][InlineData("Show me the head of the plane.","رئيس الطائرة")][InlineData("Never mind.","اقتل المحرك")][InlineData("The word prehaul assuffix is different.","اسحب المؤخرة")][InlineData("Please kill the engine.","أطفئ المحرك.")]
 public void DoesNotRewriteUnrelatedOrAlreadyCorrectOutput(string english,string arabic){Assert.Equal(arabic,CommunityCorrections.Improve("GTA 6",english,arabic));}
 [Fact]public void UnreviewedRulesCannotBeLoaded(){var json=JsonSerializer.Serialize(new{rules=new[]{new{id="bad",game="*",source_phrase="haul ass",bad_arabic="اسحب المؤخرة",replacement="أسرع",reviewed=false}}});Assert.Throws<InvalidDataException>(()=>ReviewedLanguage.Parse(json));}
 [Fact]public void RetrievedMeaningHintsAreRelevantAndReuseTheSameCloudRequest(){Assert.Contains("hurry up",AiRequest.UserMessage(["Hurry! Haul ass!"],[],"GTA6"));Assert.DoesNotContain("hurry up",AiRequest.UserMessage(["Stay here."],[],"GTA6"));}
 private sealed class MixedOutput:GameTranslator.Core.ITextTranslator {public Task<string> TranslateAsync(string text,CancellationToken token=default)=>Task.FromResult("يحتاج إلى من يحل محله الآن بعد رحيل Donnie•sgone.");}
 [Fact]public async Task MinorEnglishResidueCannotHideTheEntireArabicCaption(){
  Assert.Contains("يحتاج",await new ProfileTranslator(new MixedOutput(),"GTA6").TranslateAsync("Well, he's gonna need someone to step up now... Donnie•sgone"));
 }
 [Fact]public void UnknownResidualWordsAreMarkedWithoutHidingArabic(){Assert.Equal("افتح [غير واضح] الآن",ArabicOutputGuard.PrepareCaption("Open the smtc box now","افتح smtc box الآن"));Assert.Equal("اتصل بـ Jason عبر GPS",ArabicOutputGuard.PrepareCaption("Call Jason using GPS","اتصل بـ Jason عبر GPS"));Assert.True(ArabicOutputGuard.RejectUnusable("UUISU","UUISU"));Assert.Equal("ARTHUR: اتصل بـ D-Man عبر PS5",ArabicOutputGuard.PrepareCaption("ARTHUR: Call D-Man using PS5","ARTHUR: اتصل بـ D-Man عبر PS5"));}
 [Theory][InlineData("We should leave now.")][InlineData("ماذا؟ U U U U U U U U")][InlineData("ربما ربما ربما ربما ربما ربما ربما")]
 public void RecoveryStillRejectsUntranslatedOrDegenerateOutput(string text)=>Assert.True(ArabicOutputGuard.RejectUnusable("What? Uh",text));
 private sealed class EchoEnglish:GameTranslator.Core.ITextTranslator {public int Calls;public Task<string> TranslateAsync(string text,CancellationToken token=default){Calls++;return Task.FromResult(text);}}
 [Fact]public async Task UntranslatedSentencesAreDiagnosedAndNotRetriedOnEveryFrame(){var inner=new EchoEnglish();string? rejected=null;var profile=new ProfileTranslator(inner,"GTA6",(s,a)=>rejected=a);await Assert.ThrowsAsync<GameTranslator.Core.TranslatorException>(()=>profile.TranslateAsync("We should leave now."));await Assert.ThrowsAsync<GameTranslator.Core.TranslatorException>(()=>profile.TranslateAsync("We should leave now."));Assert.Equal(1,inner.Calls);Assert.Equal("We should leave now.",rejected);}
 [Fact]public async Task ProperNamesAndNonArabicTargetArePreserved(){var inner=new EchoEnglish();Assert.Equal("John Marston",await new ProfileTranslator(inner,"RDR2").TranslateAsync("John Marston"));Assert.Equal("We should leave now.",await new ProfileTranslator(inner,"RDR2",validateArabic:false).TranslateAsync("We should leave now."));}
}
