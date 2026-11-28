using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class LatencyRecoveryTests_063
{
    private const string Body="حسنًا. توقف جانبًا وابقَ هناك أيها الأحمق";
    [Fact]public async Task RecordedLuciaAnswerIsNotRepeatedInsideOneDisplayedResult()
    {
        const string source="Lucia: Okay. pull over and stay there assholel";
        var profile=new ProfileTranslator(new Engine("لوسيا: "+Body+" "+Body),"GTA6");
        Assert.Equal("لوسيا: "+Body,await profile.TranslateAsync(source));
        Assert.Equal("لوسيا: "+Body,Assert.Single(await profile.TranslateManyAsync([source])));
    }
    [Theory]
    [InlineData("Wait here. Wait here.","انتظر هنا رجاءً حتى أعود. انتظر هنا رجاءً حتى أعود.")]
    [InlineData("Lucia: Stop now.\nJason: Stop now.","لوسيا: توقف الآن ولا تتحرك من مكانك.\nجيسون: توقف الآن ولا تتحرك من مكانك.")]
    [InlineData("There are two cars and three people.","هناك سيارتان في هذا المكان. هناك ثلاث سيارات في هذا المكان.")]
    [InlineData("Do not move.","لا تتحرك من هذا المكان الآن. تحرك من هذا المكان الآن.")]
    public void IntentionalRepetitionSeparateRepliesNumbersAndNegationArePreserved(string source,string arabic)=>Assert.Equal(arabic,ArabicRepetition.Normalize(source,arabic));
    [Fact]public async Task RecordedLeadingLDoesNotTurnLuciaIntoAnotherSpeaker()
    {
        const string game="Grand Theft Auto VI";var a="Lucia: Move! Some ofus know where we're going!";var b="L Lucia: Move! Some of'us know where we're going!";
        var x=DialogueProfile.DialogueOnly(Reading.FromText(a),new(0,0,1,1),game).Text;var y=DialogueProfile.DialogueOnly(Reading.FromText(b),new(0,0,1,1),game).Text;
        long at=0;var engine=new CountingEngine();var pipeline=new SubtitlePipeline(new SequenceReader(x,y),engine,new Sender(),clock:()=>at){GuardDamagedRereads=true,KeepRapidReplies=true};
        await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();at=200;await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();Assert.Equal(1,engine.Calls);
    }
    private sealed class Engine(string output):ITextTranslator{public Task<string> TranslateAsync(string source,CancellationToken token=default)=>Task.FromResult(output);}
    private sealed class CountingEngine:ITextTranslator{public int Calls;public Task<string> TranslateAsync(string source,CancellationToken token=default){Calls++;return Task.FromResult("لوسيا: تحركوا! بعضنا يعرف إلى أين نحن ذاهبون!");}}
    private sealed class SequenceReader(params string[] lines):ITextReader{private int i;public string Read(Frame frame)=>lines[Math.Min(i++,lines.Length-1)];}
    private sealed class Sender:ISubtitleSender{public void SendSubtitle(string text,int durationMs=15000,TextBox? box=null,bool paragraphs=false,bool replaces=false){}public void Clear(){}}
}
