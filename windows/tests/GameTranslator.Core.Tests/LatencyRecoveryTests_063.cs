using GameTranslator.Core;
namespace GameTranslator.Core.Tests;

public class LatencyRecoveryTests_063
{
    [Theory]
    [InlineData(50,0,50)][InlineData(50,30,20)][InlineData(50,80,1)]
    [InlineData(16,10,6)][InlineData(50,-1,50)][InlineData(50,long.MaxValue,1)]
    public void CaptureWorkConsumesItsIntervalInsteadOfAddingAnotherWait(int interval,long work,int expected)=>Assert.Equal(expected,CaptureCadence.RemainingMs(interval,work));

    [Fact]public async Task FirstObservationIncludesStabilizationAndKeepsLegacyTimingExplicit()
    {
        var clock=new TestClock{Now=0};var engine=new TimedEngine(clock);var messages=new List<string>();var pipeline=new SubtitlePipeline(new Reader(clock),engine,new FakeSender(),clock:clock.Read){MinimumStableMs=120,Log=messages.Add};
        var frame=Frame.Blank(32,32);frame=new(frame.Pixels,frame.Width,frame.Height,frame.Stride){CaptureBackend="wgc",CaptureMs=7};
        await pipeline.ProcessAsync(frame,new(),0);await pipeline.WaitForTranslationsAsync();Assert.Empty(engine.Requested);
        clock.Now=150;await pipeline.ProcessAsync(frame,new(),150);await pipeline.WaitForTranslationsAsync();
        Assert.True(pipeline.LastMetrics is not null,string.Join("\n",messages));var m=pipeline.LastMetrics!;Assert.Equal(320,m.TotalMs);Assert.Equal(470,m.FirstObservedTotalMs);Assert.Equal(170,m.BeforeRequestMs);Assert.Equal(0,m.PipelineWaitMs);Assert.Equal(7,m.CaptureMs);Assert.Equal("wgc",m.CaptureBackend);
    }
    [Fact]public void CropAndResizeRetainCaptureDiagnostics()
    {
        var f=Frame.Blank(80,80);f=new(f.Pixels,80,80,f.Stride){CaptureBackend="wgc",CaptureMs=9};
        var crop=f.Crop(10,10,40,40).Scaled(2);Assert.Equal("wgc",crop.CaptureBackend);Assert.Equal(9,crop.CaptureMs);
    }
    [Theory]
    [InlineData("SELL VALUE\nREGISTRATION FEE")]
    [InlineData("PAY 'N' SPUY - DOWNTOWN\nGarage offering repairs and resprays.\nSELL VALUE\nREGISTRATION FEE")]
    [InlineData("FLEECA - TEQUESTA\nSmall bank with marked bills on site.\nFEATURES\nSECURITY\nTAKE VALUE\nCASH DRAWER\nVAULT")]
    public void RecordedMapPanelsWithoutTheirNavigationHeaderAreNotPhoneMessages(string source)=>Assert.True(PhoneReadingEvidence.Incompatible(Reading.FromText(source)));
    [Theory]
    [InlineData("Brian\nNew Destination\nCal's fishing spot\nLeft the Phoenix at your place.")]
    [InlineData("Meet me at the store. Check the sell value and registration fee.")]
    [InlineData("FEATURES\nThe place looks great.")]
    public void RealPhoneMessagesAndNavigationAreNotRejectedByMapFields(string source)=>Assert.False(PhoneReadingEvidence.Incompatible(Reading.FromText(source)));

    [Fact]public async Task RejectedMapTitleInABatchCannotHideTheDescriptionAndFields()
    {
        long at=0;var engine=new MapEngine();var reader=new MapReader();var area=ReadingArea.MapCard() with{Source=new(0,1,0,1),Structured=true};
        using var session=new ReadingAreaSession([area],()=>reader,engine,()=>at);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);session.Caption+=c=>{if(c.Text.Contains("مصرف صغير")&&c.Text.Contains("الأمن"))shown.TrySetResult(c);};
        await session.ProcessAsync(Frame.Blank(640,360));at=500;await session.ProcessAsync(Frame.Blank(640,360));
        var caption=await shown.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.NotNull(caption.Panel);Assert.Contains("Small bank with marked bills on site.",engine.Requested);Assert.DoesNotContain("FLEECA",caption.Text);
    }
    private sealed class Reader(TestClock clock):ITextReader
    {
        public string Read(Frame frame){clock.Now+=20;return "Lucia: We need to leave now.";}
    }
    private sealed class TimedEngine(TestClock clock):ITextTranslator
    {
        public readonly List<string> Requested=[];
        public Task<string> TranslateAsync(string source,CancellationToken token=default){Requested.Add(source);clock.Now+=300;return Task.FromResult("لوسيا: علينا المغادرة الآن.");}
    }
    private sealed class MapReader:ITextReader
    {
        public string Read(Frame frame)=>ReadSpatial(frame).Text;
        public Reading ReadSpatial(Frame frame)=>new([
            new("FLEECA - TEQUESTA",new(.1,.05,.8,.12)),
            new("Small bank with marked bills on site.",new(.1,.22,.8,.3)),
            new("SECURITY",new(.1,.55,.5,.61))]);
    }
    private sealed class MapEngine:ITextTranslator
    {
        public int PreferredBatchSize=>4;public readonly List<string> Requested=[];
        public Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> text,CancellationToken token=default)=>Task.FromException<IReadOnlyList<string>>(new TranslatorException(TranslatorStatus.OutputRejected));
        public Task<string> TranslateAsync(string source,CancellationToken token=default){Requested.Add(source);return source.Contains("FLEECA")?Task.FromException<string>(new TranslatorException(TranslatorStatus.OutputRejected)):Task.FromResult(source.Contains("Small bank")?"مصرف صغير يحتوي على أوراق نقدية معلّمة.":"الأمن");}
    }
}
