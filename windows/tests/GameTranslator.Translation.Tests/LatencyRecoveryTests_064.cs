using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class LatencyRecoveryTests_064
{
    [Fact]public async Task TheRecordedFourWordJasonOutputIsNotRepeated()
    {
        var p=new ProfileTranslator(new Engine("جيسون: حسناً، لاحقاً يا بريك حسناً، لاحقاً يا بريك"),"GTA6");
        Assert.Equal("جيسون: حسناً، لاحقاً يا بريك",await p.TranslateAsync("Jason: Aright later. brec"));
    }
    [Theory]
    [InlineData("Help Dre'Quan enact his plan to get back at DWNPLY.","ساعد Dre'Quan على تنفيذ خطته للانتقام من DWNPLY.","ساعد دري كوان على تنفيذ خطته للانتقام من داونبلاي.")]
    [InlineData("A local resident said San4San move a lot of money through this recording studio.","قال أحد السكان إن San4San ينقل أموالًا كثيرة عبر هذا الاستوديو.","قال أحد السكان إن سان فور سان ينقل أموالًا كثيرة عبر هذا الاستوديو.")]
    [InlineData("MEET DRE'QUÅN","قابل DRE'QUÅN","قابل دري كوان")]
    [InlineData("JACK OF HEARTS","JACK OF HEARTS","جاك أوف هارتس")]
    [InlineData("Housing the Only Raw Records recording studio, with XXX adult entertainment.","يضم استوديو Only Raw Records وترفيهًا للكبار بتصنيف XXX.","يضم استوديو أونلي راو ريكوردز وترفيهًا للكبار بتصنيف XXX.")]
    public async Task RecordedMapNamesDoNotRejectAnOtherwiseArabicParagraph(string source,string provider,string expected)
    {
        var p=new ProfileTranslator(new Engine(provider),"GTA6",joinLines:false);Assert.Equal(expected,Assert.Single(await p.TranslateManyAsync([source])));
    }
    [Fact]public async Task RejectedSpatialOutputIsAvailableForTheDiagnosticLogButNeverReturnedForDisplay()
    {
        var p=new ProfileTranslator(new Engine("هذا unfinished النص."),"GTA6");Assert.Equal("",Assert.Single(await p.TranslateManyAsync(["A complete source sentence."])));Assert.Equal("هذا unfinished النص.",p.RejectedOutput("A complete source sentence."));
    }
    [Fact]public void TerminologyDoesNotAllowUnrelatedEnglishOrInventANamedEntity()
    {
        Assert.False(ArabicOutputQuality.Accepts("Come over here.","تعال إلى DWNPLY."));Assert.False(ArabicOutputQuality.Accepts("Move now.","تحرك now."));Assert.Equal("تحرك الآن.",ArabicRepetition.Normalize("Go, go!","تحرك الآن."));
    }
    [Fact]public void AWordRepeatedInsideTheOriginalDoesNotKeepADuplicateOfItsEntireTranslation()
    {
        const string unit="نعم، نعم، ربما في وقت آخر.";
        Assert.Equal(unit,ArabicRepetition.Normalize("Yeah yeah maybe some other time.",unit+" "+unit));
        Assert.Equal(unit+" "+unit,ArabicRepetition.Normalize("Maybe some other time. Maybe some other time.",unit+" "+unit));
    }
    private sealed class Engine(string output):ITextTranslator{public Task<string> TranslateAsync(string text,CancellationToken token=default)=>Task.FromResult(output);}
}
