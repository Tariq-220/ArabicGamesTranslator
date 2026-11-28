using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class LatencyRecoveryTests_061
{
    [Theory]
    [InlineData("EXIT O")][InlineData("PHOTO STUDIO O SELEC")][InlineData("BACK O")]
    [InlineData("SELEI")][InlineData("TO TRUNK (215) O")][InlineData("SURFACE O")]
    public async Task MissionAreaDoesNotRequestEngineForRecordedMenuControls(string text)
    {
        long now=0;var engine=new Engine();
        using var s=new ReadingAreaSession([ReadingArea.Instructions()],()=>new Reader(text),engine,()=>now);
        await s.ProcessAsync(Frame.Blank(320,180));now=500;await s.ProcessAsync(Frame.Blank(320,180));
        Assert.False(engine.Entered.Task.IsCompleted);Assert.Equal(0,engine.Calls);
    }
    [Theory][InlineData("PFISTER")][InlineData("NAGASAKI\nCARBON RS")][InlineData("Map updated")][InlineData("J Jason only")]
    public async Task MapCardDoesNotRequestEngineForRecordedVehicleOrHud(string text)
    {
        long now=0;var engine=new Engine();
        using var s=new ReadingAreaSession([ReadingArea.MapCard()],()=>new Reader(text),engine,()=>now);
        await s.ProcessAsync(Frame.Blank(320,180));now=500;await s.ProcessAsync(Frame.Blank(320,180));
        Assert.False(engine.Entered.Task.IsCompleted);Assert.Equal(0,engine.Calls);
    }
    [Theory][InlineData(false,"Find Jason")][InlineData(false,"Get in the car.")]
    [InlineData(true,"Coming home\nWith Lucia out of prison, it's time to catch up.")]
    public async Task RealObjectiveAndMapProseStillReachTheEngine(bool map,string text)
    {
        long now=0;var engine=new Engine();
        using var s=new ReadingAreaSession([map?ReadingArea.MapCard():ReadingArea.Instructions()],()=>new Reader(text),engine,()=>now);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);
        s.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};
        await s.ProcessAsync(Frame.Blank(320,180));now=500;await s.ProcessAsync(Frame.Blank(320,180));
        Assert.NotEmpty((await shown.Task.WaitAsync(TimeSpan.FromSeconds(3))).Text);
    }
    [Theory][InlineData("PROGRESS\nBRIEF\nROBBERY OPPORTUNITY")][InlineData("BRIEF\nPROGRESS\nVEHICLE KNOWN")]
    public void EitherNavigationOrderIsNotAPhoneConversation(string text)=>Assert.True(PhoneReadingEvidence.Incompatible(Reading.FromText(text)));
    [Theory][InlineData("-E Lucia: Chill, bro.","Lucia: Chill, bro.")]
    [InlineData("Ji Tourist: Ohh, that's real cute!","Tourist: Ohh, that's real cute!")]
    [InlineData("s Staff Member: Don't!","Staff Member: Don't!")]
    [InlineData("Mr Jason: Good evening.","Mr Jason: Good evening.")]
    [InlineData("Young Jason: Good evening.","Young Jason: Good evening.")]
    [InlineData("José: Good evening.","José: Good evening.")]
    public void RecordedOcrPrefixesAreRemovedWithoutInventingUnknownNames(string source,string expected)=>Assert.Equal(expected,ArabicGameNames.NormalizeSpeakerLabels(source));
    [Fact]public async Task RejectedOutputBacksOffButANewDialogueIsNotDelayed()
    {
        long now=0;var reader=new Reader("A damaged phrase.");var engine=new Rejecting();var sender=new FakeSender();
        var p=new SubtitlePipeline(reader,engine,sender,clock:()=>now);
        foreach(var at in new[]{0,500,1100,1600,2200,2700}){now=at;await p.ProcessAsync(Frame.Blank(32,32),new());await p.WaitForTranslationsAsync();}
        Assert.Equal(2,engine.Calls);
        reader.Text="Jason: Get in the car!";now=2800;
        await p.ProcessAsync(Frame.Blank(32,32),new());await p.WaitForTranslationsAsync();
        Assert.Equal(3,engine.Calls);Assert.Equal("اركب السيارة!",p.LastPair?.Translation);
    }
    [Fact]public async Task PixelIdenticalInactiveAreaReusesOcrButChangedPixelsAreReadImmediately()
    {
        long now=0;var reader=new CountingReader();
        using var s=new ReadingAreaSession([ReadingArea.MapCard() with{Source=new(0,1,0,1),IntervalMs=16}],()=>reader,new Engine(),()=>now){ReuseIdenticalFrames=true};
        var image=Frame.Blank(64,64);await s.ProcessAsync(image);now=100;await s.ProcessAsync(image);now=200;await s.ProcessAsync(image);
        Assert.Equal(1,reader.Calls);
        var changed=Frame.Blank(64,64);changed.Pixels[200]=42;now=300;await s.ProcessAsync(changed);Assert.Equal(2,reader.Calls);
        now=1400;await s.ProcessAsync(changed);Assert.Equal(3,reader.Calls);
    }
    private sealed class CountingReader:ITextReader
    {public int Calls;public string Read(Frame frame)=>"";public Reading ReadSpatial(Frame frame){Calls++;return Reading.FromText("PFISTER");}}
    [Fact]public async Task VerifiedPhoneReservesItsPhysicalMessageSpaceEvenWhenOtherOcrTextDiffers()
    {
        long now=0;var reader=new SpatialPhone();var image=Frame.Blank(300,600);Array.Fill(image.Pixels,(byte)235);
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{Source=new(0,1,0,1),AutoDetect=false}],()=>reader,new Engine(),()=>now);
        await s.ProcessAsync(image);
        var reading=new Reading([new("Almost the re",new(.12,.25,.78,.29)),new("Get in the car",new(.1,.9,.8,.96))]);
        var remaining=s.WithoutMessageText(reading,new(0,0,1,1),false);
        Assert.Equal("Get in the car",Assert.Single(remaining.Lines).Text);
        Assert.Equal("Jason: Get in!",s.WithoutMessageText(new([new("Jason: Get in!",new(.1,.25,.8,.3))]),new(0,0,1,1)).Text);
    }
    [Fact]public async Task OpeningPhoneCancelsAnAlreadyRunningMissionRequestForItsMessage()
    {
        long now=0;int made=0;var blocked=new CancelledMission();var image=Frame.Blank(300,600);Array.Fill(image.Pixels,(byte)235);
        var mission=ReadingArea.Instructions() with{Source=new(0,1,0,1),Structured=false};var phone=ReadingArea.Phone() with{Source=new(0,1,0,1),AutoDetect=false,IntervalMs=2000};
        using var s=new ReadingAreaSession([mission,phone],()=>made++==0?new SpatialMission():new EmptyThenPhone(),blocked,()=>now);
        var displayed=new List<AreaCaption>();s.Caption+=c=>{if(!c.Clear&&c.Id==mission.Id)displayed.Add(c);};
        await s.ProcessAsync(image);now=100;await s.ProcessAsync(image);now=500;await s.ProcessAsync(image);
        await blocked.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));now=2200;await s.ProcessAsync(image);
        await blocked.Cancelled.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Empty(displayed);
    }
    private sealed class SpatialPhone:ITextReader
    {public string Read(Frame f)=>"Jason\nAlmost there!\nI'm on it.";public Reading ReadSpatial(Frame f)=>new([new("Jason",new(.3,.02,.7,.08)),new("Almost there!",new(.1,.2,.8,.3)),new("I'm on it.",new(.2,.5,.9,.6))]);}
    private sealed class SpatialMission:ITextReader
    {public string Read(Frame f)=>"Almost there!";public Reading ReadLines(Frame f,bool deep)=>new([new("Almost there!",new(.1,.2,.8,.3))]);}
    private sealed class EmptyThenPhone:ITextReader
    {private int calls;public string Read(Frame f)=>"";public Reading ReadSpatial(Frame f)=>++calls==1?Reading.Empty:new SpatialPhone().ReadSpatial(f);}
    private sealed class CancelledMission:ITextTranslator
    {public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Cancelled=new(TaskCreationOptions.RunContinuationsAsynchronously);
     public async Task<string> TranslateAsync(string text,CancellationToken token=default){Entered.TrySetResult();try{await Task.Delay(Timeout.Infinite,token);}catch(OperationCanceledException){Cancelled.TrySetResult();throw;}return "";}}
    private sealed class Reader(string text):ITextReader
    {
        public string Text=text;
        public string Read(Frame f)=>Text;
        public Reading ReadLines(Frame f,bool deep)=>Reading.FromText(Text);
        public Reading ReadSpatial(Frame f)=>new(Text.Split('\n').Select((s,i)=>new TextLine(s,new(.1,.1+i*.15,.9,.18+i*.15))));
    }
    private sealed class Engine:ITextTranslator
    {
        public int Calls;public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string text,CancellationToken token=default){Interlocked.Increment(ref Calls);Entered.TrySetResult();return Task.FromResult("ترجمة سليمة للمهمة.");}
    }
    private sealed class Rejecting:ITextTranslator
    {
        public int Calls;public Task<string> TranslateAsync(string text,CancellationToken token=default){Calls++;return text.Contains("damaged")?Task.FromException<string>(new TranslatorException(TranslatorStatus.OutputRejected)):Task.FromResult("اركب السيارة!");}
    }
}
