using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class ReadingAreaTests_060
{
    private sealed class Reader(Reading lines):ITextReader{public Reading Lines=lines;public string Read(Frame f)=>Lines.Text;public Reading ReadSpatial(Frame f)=>Lines;}
    private sealed class Arabic:ITextTranslator{public int Calls;public Task<string> TranslateAsync(string s,CancellationToken t=default){Interlocked.Increment(ref Calls);return Task.FromResult("رسالة عربية سليمة.");}}
    private static Frame Light(int width=300,int height=600){var f=Frame.Blank(width,height);Array.Fill(f.Pixels,(byte)235);return f;}
    [Theory][InlineData(true)][InlineData(false)]
    public async Task RecordedGameplayCannotBePublishedOrOwnedByAManualPhoneCard(bool structured)
    {
        long now=0;var source=new CropRegion(.11182121104655093,.8759766455174428,.3502806341923062,.7751391147904854);
        var local=new TextBox(.12,.88,.85,.98);var reading=new Reading([new("Locals: Motherfucker!",local)]);var engine=new Arabic();
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Structured=structured,Source=source}],()=>new Reader(reading),engine,()=>now);
        var captions=new List<AreaCaption>();s.Caption+=c=>{if(!c.Clear)captions.Add(c);};await s.ProcessAsync(Frame.Blank(1280,720));now=500;await s.ProcessAsync(Frame.Blank(1280,720));await Task.Delay(80);
        Assert.Empty(captions);Assert.Equal(0,engine.Calls);Assert.Single(s.WithoutMessageText(new([new TextLine(reading.Text,local.PlacedIn(new(source.Left,source.Top,source.Right,source.Bottom)))]),new(0,0,1,1)).Lines);
    }
    [Theory][InlineData("+289 m")][InlineData("JASON & LUCIA\nMAP\nBRIEF\nPROGRESS\nRICO HUXLEY HOTELS ROOFTOP")][InlineData("+289 m\nTourist: You are going to get someone killed!\nLocal: Die, ho!")]
    public async Task RecordedHudAndMapAreNotPhoneMessages(string text)
    {
        long now=0;var reading=new Reading(text.Split('\n').Select((x,i)=>new TextLine(x,new(.1,.05+i*.12,.9,.10+i*.12))));
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(0,1,0,1)}],()=>new Reader(reading),new Arabic(),()=>now);
        var captions=new List<AreaCaption>();s.Caption+=c=>{if(!c.Clear)captions.Add(c);};await s.ProcessAsync(Light());now=500;await s.ProcessAsync(Light());await Task.Delay(80);Assert.Empty(captions);
    }
    [Fact]public async Task APreciselyCroppedSinglePhoneMessageStillAppears()
    {
        long now=0;var lines=new Reading([new("Almost there!",new(.1,.2,.85,.3))]);
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(.15,.9,.55,.85)}],()=>new Reader(lines),new Arabic(),()=>now);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);s.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};await s.ProcessAsync(Light(900,600));now=500;await s.ProcessAsync(Light(900,600));Assert.Equal("phone",(await shown.Task.WaitAsync(TimeSpan.FromSeconds(2))).Panel!.Kind);
    }
    [Fact]public void CarPhoneCanBeSelectedSharedAndRestoredIndependently()
    {
        var phone=ReadingArea.Phone();var car=ReadingArea.CarPhone();Assert.False(phone.SamePreset(car));Assert.NotEqual(phone.Source,car.Source);Assert.NotEqual(phone.Output,car.Output);
        var dialogue=new ReadingArea("dialogue","الحوار",ReadingAreaKind.Dialogue,new(),new());var restored=ReadingLayout.Import(new ReadingLayout(1,[dialogue,phone,car]).Export()).Areas.Single(a=>a.Variant=="car");
        Assert.Equal(car.Source,restored.Source);Assert.Equal(car.Output,restored.Output);var moved=car with{Source=new(.3,.6,.2,.5),Name="داخل السيارة"};Assert.Equal(car.Source,ReadingArea.Restore(moved).Source);Assert.Equal(car.Output,ReadingArea.Restore(moved).Output);Assert.Equal("داخل السيارة",ReadingArea.Restore(moved).Name);
    }
    [Fact]public async Task AConfirmedPhoneCannotSwallowALabelledGameReplyAtTheSamePosition()
    {
        long now=0;var reading=new Reading([new("Jason",new(.3,.03,.6,.08)),new("Meet me at home.",new(.1,.5,.9,.6))]);var source=new CropRegion(.1,.9,.6,.95);
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=source}],()=>new Reader(reading),new Arabic(),()=>now);await s.ProcessAsync(Light(900,600));
        var global=reading.Lines[1].Box!.Value.PlacedIn(new(source.Left,source.Top,source.Right,source.Bottom));Assert.Single(s.WithoutMessageText(new([new TextLine("Jason: We need to leave!",global)]),new(0,0,1,1)).Lines);
    }
    [Fact]public async Task BothPhonePresetsDoNotDuplicateTheSameVisibleConversation()
    {
        long now=0;var reading=new Reading([new("Jason",new(.3,.03,.6,.08)),new("Meet me at home.",new(.1,.3,.9,.4))]);var phone=ReadingArea.Phone() with{AutoDetect=false};var car=ReadingArea.CarPhone() with{AutoDetect=false};
        using var s=new ReadingAreaSession([phone,car],()=>new Reader(reading),new Arabic(),()=>now);var active=new System.Collections.Concurrent.ConcurrentDictionary<string,AreaCaption>();s.Caption+=c=>{if(c.Clear)active.TryRemove(c.Id,out _);else active[c.Id]=c;};
        for(int i=0;i<8;i++){now=i*500;await s.ProcessAsync(Light(900,600));await Task.Delay(10);}Assert.Single(active);Assert.True(active.ContainsKey(car.Id));
    }
    [Fact]public void OcrVariationsCannotMergeDifferentNegationOrNumbers()
    {
        var t=new PhonePanelTracker();PanelCell C(string source)=>new(source,"",new(.1,.3,.8,.4),"#FAFAFA","#20232A",.03,true);
        t.Observe(new(300,600,"phone",null,[C("You should not go out.")]),0);t.Observe(new(300,600,"phone",null,[C("You should go out.")]),500);Assert.Equal("You should go out.",t.Observe(new(300,600,"phone",null,[C("You should go out.")]),1000)!.Cells.Single().Source);
        t.Observe(new(300,600,"phone",null,[C("Meet in 10 minutes.")]),1500);t.Observe(new(300,600,"phone",null,[C("Meet in 10 minutes.")]),2000);t.Observe(new(300,600,"phone",null,[C("Meet in 20 minutes.")]),2500);Assert.Equal("Meet in 20 minutes.",t.Observe(new(300,600,"phone",null,[C("Meet in 20 minutes.")]),3000)!.Cells.Single().Source);
    }
    [Fact]public async Task ARealPhoneAndGameplayDialogueInTheSameManualCropAreSeparated()
    {
        long now=0;var reading=new Reading([new("Kenton",new(.3,.03,.7,.08)),new("Meet me at home.",new(.1,.3,.9,.4)),new("Jason: Yo, eat a dick!",new(.2,.85,.8,.91))]);
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(0,1,0,1)}],()=>new Reader(reading),new Arabic(),()=>now);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);s.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};await s.ProcessAsync(Light());now=500;await s.ProcessAsync(Light());var caption=await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.DoesNotContain(caption.Panel!.Cells,c=>c.Source.StartsWith("Jason:"));Assert.Contains(caption.Panel.Cells,c=>c.Source=="Meet me at home.");Assert.Single(s.WithoutMessageText(new([reading.Lines[2]]),new(0,0,1,1)).Lines);
    }
    [Fact]public async Task UnlabelledDialogueOnTheDarkSubtitleBandIsNotAPhone()
    {
        long now=0;var line=new TextLine("And people think I'm crazy?",new(.2,.8,.8,.9));using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(0,1,0,1)}],()=>new Reader(new([line])),new Arabic(),()=>now);var captions=new List<AreaCaption>();s.Caption+=captions.Add;
        await s.ProcessAsync(Frame.Blank(1280,720));now=500;await s.ProcessAsync(Frame.Blank(1280,720));await Task.Delay(60);Assert.Empty(captions);Assert.Single(s.WithoutMessageText(new([line]),new(0,0,1,1)).Lines);
    }
    [Fact]public void AContactNameAloneCannotBecomeANotificationCard(){var t=new PhonePanelTracker();Assert.Null(t.Observe(new(300,100,"notification",null,[new("Santiago","",new(.1,.02,.8,.1),"#FFFFFF","#20232A",.04,Heading:true)]),0));}
    [Fact]public void MatchingMessageDoesNotDriftWithOcrBoxJitter()
    {
        var t=new PhonePanelTracker();var cell=new PanelCell("You guys go ahead.","",new(.1,.3,.8,.4),"#FAFAFA","#20232A",.03,true);
        t.Observe(new(300,600,"phone",null,[cell]),0);var next=t.Observe(new(300,600,"phone",null,[cell with{Box=new(.12,.33,.82,.43),LineHeight=.05}]),500)!;
        Assert.Equal(cell.Box,next.Cells.Single().Box);Assert.Equal(cell.LineHeight,next.Cells.Single().LineHeight);
    }
    [Fact]public void ATruncatedFirstReadingCanGrowWithoutBecomingAnotherPage()
    {
        var t=new PhonePanelTracker();var a=new PanelCell("Sorry, something came up.","",new(.1,.3,.8,.4),"#FAFAFA","#20232A",.03,true);t.Observe(new(300,600,"phone",null,[a]),0);
        var full=t.Observe(new(300,600,"phone",null,[a with{Source="Sorry, something came up. I'll see you later."}]),500)!;
        Assert.Equal("Sorry, something came up. I'll see you later.",full.Cells.Single().Source);Assert.Equal(a.Box,full.Cells.Single().Box);
    }
}

internal static class PhoneFrames
{
    public static Frame Light(int w,int h){var f=Frame.Blank(w,h);Array.Fill(f.Pixels,(byte)235);return f;}
}
