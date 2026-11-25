using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class SubtitlePipelineTests_057
{
    [Fact]
    public async Task OnlyConfirmedSourceRenewsAShortDisplayLeaseWithoutMoreInference()
    {
        long now=0;var engine=new FakeTranslator();var sender=new FakeSender();
        var pipeline=new SubtitlePipeline(new FakeReader("Hold the gate.","Hold the gate.","",""),engine,sender,clock:()=>now){ExpireWhenSourceGone=true};
        int shown=0,renewed=0;pipeline.PairShown+=(_,_)=>shown++;pipeline.CaptionRenewed+=()=>renewed++;
        await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync();
        Assert.InRange(sender.Durations[0],1500,2500);
        now=600;await pipeline.ProcessAsync(Frame.Blank(64,64),new());
        Assert.Equal(2,sender.Sent.Count);Assert.Single(engine.Requested);Assert.Equal(1,shown);Assert.Equal(1,renewed);
        now=1200;await pipeline.ProcessAsync(Frame.Blank(64,64),new());
        Assert.Equal(2,sender.Sent.Count);
        now=2100;await pipeline.ProcessAsync(Frame.Blank(64,64),new());Assert.Equal(1,sender.Clears);
    }
    [Fact]
    public async Task OcrNoiseDoesNotRenewOldDialogueForTenSeconds()
    {
        long now=0;var sender=new FakeSender();var pipeline=new SubtitlePipeline(new FakeReader("Come here now!","|||","???"),new FakeTranslator(),sender,clock:()=>now){ExpireWhenSourceGone=true};
        await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync();
        now=700;await pipeline.ProcessAsync(Frame.Blank(64,64),new());now=1600;await pipeline.ProcessAsync(Frame.Blank(64,64),new());
        Assert.Single(sender.Sent);Assert.InRange(sender.Durations[0],1500,2500);
    }
    [Fact]
    public async Task OcrSpeakerVariationCannotStackTheSameArabicTwice()
    {
        long now=0;var pipeline=new SubtitlePipeline(new FakeReader("Billy: I know, I know.","I know, I know."),new SameArabic(),new FakeSender(),clock:()=>now){KeepRapidReplies=true,MinimumDisplayMs=700};
        await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync();now=200;await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync();
        Assert.Equal("أعرف، أعرف.",pipeline.DisplayText);
    }
    private sealed class SameArabic:ITextTranslator{public Task<string> TranslateAsync(string s,CancellationToken t=default)=>Task.FromResult("أعرف، أعرف.");}
}
