using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class SubtitlePipelineTests_Recovery
{
    [Fact]public async Task ReadabilityWindowDoesNotDelayNextInferenceAndReplyAppearsBelowPrevious(){
        long now=0;var sender=new FakeSender();var engine=new FakeTranslator();var pipeline=new SubtitlePipeline(new FakeReader("Hold the gate.","Get down now!"),engine,sender,clock:()=>now){MinimumDisplayMs=3000,KeepRapidReplies=true};
        await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync();now=100;await pipeline.ProcessAsync(Frame.Blank(64,64),new());await pipeline.WaitForTranslationsAsync().WaitAsync(TimeSpan.FromMilliseconds(1000));Assert.Equal(2,engine.Requested.Count);Assert.Equal("TR: Hold the gate.\nTR: Get down now!",sender.Sent[^1]);Assert.Equal("Get down now!",pipeline.LastPair!.Value.Source);
    }
    [Fact]public async Task TranslationCannotReappearAfterConfirmedSourceDisappearance(){
        long now=0;var sender=new FakeSender();var engine=new GatedTranslator();var pipeline=new SubtitlePipeline(new FakeReader("Hold the gate.","",""),engine,sender,clock:()=>now){ExpireWhenSourceGone=true,MissingTextMs=100};int clears=0;pipeline.CaptionCleared+=()=>clears++;await pipeline.ProcessAsync(Frame.Blank(64,64),new());now=10;await pipeline.ProcessAsync(Frame.Blank(64,64),new());now=111;await pipeline.ProcessAsync(Frame.Blank(64,64),new());engine.Open();await pipeline.WaitForTranslationsAsync();Assert.Empty(sender.Sent);Assert.Equal(1,clears);
    }
    [Fact]public void ShortReplyAndDifferentSpeakerAreNotDiscardedAsNoiseOrReread(){long now=0;var s=new SubtitleStabilizer(clock:()=>now);Assert.Equal(StabilizerKind.Translate,s.Feed("Jason: No.").Kind);now=100;Assert.Equal(StabilizerKind.Translate,s.Feed("Lucia: No.").Kind);Assert.Equal(StabilizerKind.Translate,new SubtitleStabilizer().Feed("No!").Kind);Assert.False(TextMatch.IsRevision("Lucia: Stay right here.","Jason: Stay right here."));}
}
