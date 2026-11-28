using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class LatencyRecoveryTests_062
{
    [Theory]
    [InlineData("Sucia","Lucia","Wow, too easy.„")]
    [InlineData("ILucia","Lucia","Damn it! They saw the car.")]
    [InlineData("Nason","Jason","Oh, you gonna teach him?")]
    [InlineData("Aucia","Lucia","Hey, I wouldn't mind a little, like, sailing boat or something...")]
    public async Task RecordedLabelFlickerDoesNotRepeatOrStackTheDialogue(string damaged,string name,string body)
    {
        long now=0;var source=name+": "+body;var engine=new ArabicEngine();var sender=new FakeSender();
        var p=new SubtitlePipeline(new FakeReader(source,damaged+": "+body,body,source,damaged+": "+body),engine,sender,clock:()=>now){GuardDamagedRereads=true,KeepRapidReplies=true,MinimumDisplayMs=700};
        for(int i=0;i<5;i++){now=i*200;await p.ProcessAsync(Frame.Blank(64,64),new());await p.WaitForTranslationsAsync();}
        Assert.Equal(new[]{source},engine.Requested);Assert.Single(sender.Sent);Assert.Equal("الترجمة العربية السليمة.",p.DisplayText);
    }
    [Theory]
    [InlineData("Jason: We need to leave.")]
    [InlineData("Lucia: We do not need to leave.")]
    [InlineData("Luis: We need to leave.")]
    public async Task ARealReplyOrDifferentSpeakerStillReachesTheEngine(string reply)
    {
        long now=0;var engine=new FakeTranslator();var p=new SubtitlePipeline(new FakeReader("Lucia: We need to leave.",reply),engine,new FakeSender(),clock:()=>now){GuardDamagedRereads=true};
        await p.ProcessAsync(Frame.Blank(64,64),new());await p.WaitForTranslationsAsync();now=150;
        await p.ProcessAsync(Frame.Blank(64,64),new());await p.WaitForTranslationsAsync();
        Assert.Equal(2,engine.Requested.Count);Assert.Equal(reply,engine.Requested[1]);
    }
    [Fact]public void SpeakerRepairRequiresARecentConfirmedIdenticalSentence()
    {
        var continuity=new SpeakerReadingContinuity();var source="Lucia: We need to leave.";
        Assert.Equal("Sucia: We need to leave.",continuity.Filter("Sucia: We need to leave.",0));
        continuity.Filter(source,10);
        Assert.Equal("Sucia: We need to leave?",continuity.Filter("Sucia: We need to leave?",50));
        Assert.Equal("Sucia: We need to leave.",continuity.Filter("Sucia: We need to leave.",1600));
        continuity.Filter(source,1700);continuity.Reset();
        Assert.Equal("We need to leave.",continuity.Filter("We need to leave.",1800));
    }
    [Theory]
    [InlineData("PHOTO STUDIO O SELEC'")][InlineData("PHOTO STIJDIO O SELEC")]
    [InlineData("RESTöRE ALLO RESTORE DEFÅÜL'\nPAUSE PREVIEW")]
    [InlineData("•a ct")][InlineData("NIN")]
    public async Task RecordedMapAndSettingsFooterNeverRequestsMissionTranslation(string text)
    {
        Assert.Empty(AuxiliaryReadingEvidence.Select(Reading.FromText(text),ReadingAreaKind.Instructions).Lines);
        long now=0;var engine=new FakeTranslator();using var session=new ReadingAreaSession([ReadingArea.Instructions()],()=>new FakeReader(text,text,text),engine,()=>now);
        for(int i=0;i<3;i++){now=i*600;await session.ProcessAsync(Frame.Blank(320,180));}
        Assert.Empty(engine.Requested);
    }
    [Theory][InlineData("Go")][InlineData("Find Jason")][InlineData("Select a getaway vehicle.")][InlineData("Restore power to the building.")]
    public void GenuineObjectivesSurviveTheControlFilter(string text)=>Assert.Equal(text,AuxiliaryReadingEvidence.Select(Reading.FromText(text),ReadingAreaKind.Instructions).Text);
    [Fact]public async Task DisablingWhileOcrIsRunningCannotStartAProviderRequest()
    {
        var area=ReadingArea.Instructions();var reader=new BlockingReader();var engine=new FakeTranslator();
        using var session=new ReadingAreaSession([area],()=>reader,engine);
        var processing=session.ProcessAsync(Frame.Blank(320,180));await reader.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));
        session.UpdateAreas([area with{Enabled=false}]);reader.Release.Set();await processing;
        Assert.Empty(engine.Requested);
    }
    [Theory]
    [InlineData("Heads up. Safe is in his office. There is a cover.",.2,true)]
    [InlineData("Heads up. Safe is not in his office.",.2,false)]
    [InlineData("The getaway vehicle is ready.",.2,false)]
    [InlineData("Heads up. Safe is in his office.",.5,false)]
    public void PendingPhoneExpansionKeepsReadableArabicWithoutCachingItForTheNewText(string source,double top,bool retain)
    {
        var old=new PanelCell("Heads up. Safe is in his office.","",new(.1,.2,.9,.3),"#FFFFFF","#20232A",.03,true);
        var shown=old with{Text="انتبه. الخزنة في مكتبه."};var raw=new PanelLayout(400,800,"phone",null,[old]);var display=raw with{Cells=[shown]};
        var next=old with{Source=source,Box=new(.1,top,.9,top+.1)};var cache=new Dictionary<string,string>();
        Assert.Equal(retain?shown.Text:"",ReadingAreaSession.VisibleCellTranslation(next,cache,raw,display,true));Assert.Empty(cache);
        Assert.Empty(ReadingAreaSession.VisibleCellTranslation(next,cache,raw,display,false));
        cache[TextMatch.Normalize(source)]="ترجمة الرسالة المكتملة.";
        Assert.Equal("ترجمة الرسالة المكتملة.",ReadingAreaSession.VisibleCellTranslation(next,cache,raw,display,true));
    }
    [Fact]public async Task VisiblePhoneMessageDoesNotDisappearWhileItsFullerReadingIsTranslated()
    {
        long now=0;var line=new TextLine("Sorry, something came up.",new(.1,.3,.9,.4));var reader=new PhoneReader(new([new("Kenton",new(.3,.03,.7,.08)),line]));var engine=new PhoneEngine();
        using var session=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(.1,.9,.6,.95)}],()=>reader,engine,()=>now);
        var early=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);var pending=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);var full=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);
        session.Caption+=c=>{if(c.Panel?.Cells.FirstOrDefault(x=>!x.Heading) is not {} body||body.Text.Length==0)return;if(!body.Source.EndsWith("later."))early.TrySetResult(c);else if(body.Text=="عذرًا، طرأ أمر.")pending.TrySetResult(c);else full.TrySetResult(c);};
        var frame=Frame.Blank(900,600);Array.Fill(frame.Pixels,(byte)255);
        await session.ProcessAsync(frame);now=500;await session.ProcessAsync(frame);await early.Task.WaitAsync(TimeSpan.FromSeconds(2));
        reader.Lines=new([new("Kenton",new(.3,.03,.7,.08)),line with{Text="Sorry, something came up. I'll see you later."}]);
        now=1000;await session.ProcessAsync(frame);now=1500;await session.ProcessAsync(frame);
        await engine.Started.Task.WaitAsync(TimeSpan.FromSeconds(2));var visible=await pending.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.Contains(visible.Panel!.Cells,c=>c.Text=="عذرًا، طرأ أمر.");
        engine.Release.TrySetResult("عذرًا، طرأ أمر. سأراك لاحقًا.");var complete=await full.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.Contains(complete.Panel!.Cells,c=>c.Text.Contains("سأراك لاحقًا"));
    }
    private sealed class PhoneReader(Reading initial):ITextReader
    {
        public Reading Lines=initial;public string Read(Frame frame)=>Lines.Text;public Reading ReadSpatial(Frame frame)=>Lines;
    }
    private sealed class PhoneEngine:ITextTranslator
    {
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public readonly TaskCompletionSource<string> Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string source,CancellationToken token=default){if(source.EndsWith("later.")){Started.TrySetResult();return Release.Task.WaitAsync(token);}return Task.FromResult(source=="Kenton"?"كينتون":"عذرًا، طرأ أمر.");}
    }
    private sealed class BlockingReader:ITextReader
    {
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);public readonly ManualResetEventSlim Release=new();
        public string Read(Frame frame){Started.TrySetResult();Release.Wait(TimeSpan.FromSeconds(2));return "Follow the marked road.";}
    }
    private sealed class ArabicEngine:ITextTranslator
    {
        public List<string> Requested=[];
        public Task<string> TranslateAsync(string source,CancellationToken token=default){Requested.Add(source);return Task.FromResult("الترجمة العربية السليمة.");}
    }
}
