using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class LatencyRecoveryTests_064
{
    [Fact]public async Task APlainMapKeepsTranslatedProseWhenTheSeparateTitleIsRejected()
    {
        long at=0;var engine=new MapEngine();var area=ReadingArea.MapCard() with{Source=new(0,1,0,1),Structured=false,IntervalMs=16};
        var reader=new SpatialReader("UNKNOWN VENUE\nA small place that sells\nfood and drinks.\nFEATURES");
        using var session=new ReadingAreaSession([area],()=>reader,engine,()=>at);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);var trace=new System.Collections.Concurrent.ConcurrentBag<AreaTrace>();
        var final=new TaskCompletionSource<string>(TaskCreationOptions.RunContinuationsAsynchronously);session.Status+=(_,status)=>{if(status.Contains("بعض الأجزاء"))final.TrySetResult(status);};
        session.Caption+=c=>{if(c.Text.Contains("مكان صغير"))shown.TrySetResult(c);};session.Trace+=trace.Add;
        await session.ProcessAsync(Frame.Blank(640,400));at=150;await session.ProcessAsync(Frame.Blank(640,400));
        var caption=await shown.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Null(caption.Panel);Assert.Equal(area.Output,caption.Output);
        Assert.Contains("A small place that sells food and drinks.",engine.Requested);Assert.DoesNotContain(reader.Source.Text,engine.Requested);Assert.DoesNotContain("UNKNOWN VENUE",caption.Text);
        Assert.StartsWith("تُعرض 2 أجزاء",await final.Task.WaitAsync(TimeSpan.FromSeconds(3)));Assert.Contains(trace,t=>t.Stage=="rejected_output"&&t.Translation=="UNKNOWN VENUE");
    }
    [Fact]public void MapWrapsStayTogetherAndRecordedIconFragmentsAreNotSentAsWords()
    {
        Assert.Equal(new[]{"MEET DRE'QUÅN","Jason only","Help Dre'Quan enact his plan to get back at DWNPLY."},MapTextPieces.Split("n MEET DRE'QUÅN\nJ Jason only\nHelp Dre'Quan enact his plan to get\nback at DWNPLY."));
    }
    [Fact]public async Task RecordedUnknownFirstSpeakerIsCorrectedWithoutStackingADuplicate()
    {
        long at=0;var sender=new FakeSender();var engine=new SpeakerEngine();
        var pipeline=new SubtitlePipeline(new FakeReader("Nason: Yeah. yeah maybe some other time.","Jason: Yeah, yeah maybe some other time."),engine,sender,clock:()=>at){GuardDamagedRereads=true,KeepRapidReplies=true,MinimumDisplayMs=700};
        await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();at=230;await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();
        Assert.Equal("جيسون: نعم، ربما في وقت آخر.",sender.Sent[^1]);Assert.DoesNotContain('\n',sender.Sent[^1]);Assert.True(sender.Replaces[^1]);
    }
    [Fact]public async Task RecordedPunctuationInDriverLabelDoesNotBecomeANewSpeaker()
    {
        long at=0;var engine=new SpeakerEngine();var pipeline=new SubtitlePipeline(new FakeReader("Driver: Who do you think you are?","Dl'iver: Who do you think you are?"),engine,new FakeSender(),clock:()=>at){GuardDamagedRereads=true};
        await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();at=60;await pipeline.ProcessAsync(Frame.Blank(32,32),new());await pipeline.WaitForTranslationsAsync();Assert.Single(engine.Requested);
    }
    [Fact]public async Task FadingFirstTurnDoesNotRepeatTheRemainingLocalReply()
    {
        const string body="Look at me! Look at this neck! My neck is stronger than your thigh, bro!";
        long at=0;var sender=new FakeSender();var p=new SubtitlePipeline(new FakeReader("Jason: How do you not lose that little guy?\nLocal: "+body,"Local: "+body),new TurnsEngine(),sender,clock:()=>at){KeepRapidReplies=true,MinimumDisplayMs=700};
        await p.ProcessAsync(Frame.Blank(32,32),new());await p.WaitForTranslationsAsync();at=560;await p.ProcessAsync(Frame.Blank(32,32),new());await p.WaitForTranslationsAsync();
        Assert.Equal("أحد السكان: انظر إليّ!",sender.Sent[^1]);Assert.True(sender.Replaces[^1]);
    }
    [Fact]public void SharedTurnReplacementKeepsDifferentSpeakersNumbersAndNegationDistinct()
    {
        Assert.False(DialogueTurnOverlap.SharesTurn("Jason: We have 1 car.\nLucia: We do not have 2 cars.","Jason: We have 2 cars.\nLucia: We have 2 cars."));
        Assert.False(DialogueTurnOverlap.SharesTurn("Jason: We should leave right now.","Lucia: We should leave right now.\nLocal: Stay right here."));
        Assert.True(DialogueTurnOverlap.SharesTurn("Jason: We should leave right now.\nLucia: I will follow you.","Jason: We should leave right now."));
        Assert.False(DialogueTurnOverlap.SharesTurn("Jason: We should leave right now?","Jason: We should leave right now.\nLucia: I will follow you."));
    }
    [Fact]public void ARecordedPartialPhoneReadKeepsTheCompleteMessageKeyAndGeometry()
    {
        const string full="Getaway vehicle is good Looks like all the rest of the armored cars at that bank";
        const string damaged="Getawav vehicle 's good Lc like all the rest of the armo cars at that bank,";
        var tracker=new PhonePanelTracker();var initial=Page("Sinfrontera",full);tracker.Observe(initial,0);
        var next=tracker.Observe(Page("Sintrontera",damaged),200)!;
        Assert.Equal(full,next.Cells[1].Source);Assert.Equal(initial.Cells[1].Box,next.Cells[1].Box);Assert.Equal("Sinfrontera",next.Cells[0].Source);
    }
    [Theory][InlineData("We do not have the car ready yet.")][InlineData("We have 2 cars ready to go now.")][InlineData("Meet me at the bank later today.")]
    public void NewPhoneContentNegationAndNumbersAreNotReplacedByAnOlderMessage(string next)
    {
        var tracker=new PhonePanelTracker();tracker.Observe(Page("Sinfrontera","We have 1 car ready to go now."),0);
        tracker.Observe(Page("Sinfrontera",next),100);var accepted=tracker.Observe(Page("Sinfrontera",next),250)!;Assert.Contains(accepted.Cells,c=>c.Source==next);
    }
    [Fact]public void ARecognizedContactUpgradesTheEarlierMisspelledHeader()
    {
        var tracker=new PhonePanelTracker();tracker.Observe(Page("Sintrontera","Meet me at the boat later."),0);
        Assert.Equal("Sinfrontera",tracker.Observe(Page("Sinfrontera","Meet me at the boat later."),100)!.Cells[0].Source);
    }
    [Fact]public void HeaderOnlyOcrBlinkIsBoundedAndNeverCarriesIntoAnotherContact()
    {
        var tracker=new PhonePanelTracker();var initial=Page("Jason","We should leave right now.");tracker.Observe(initial,0);
        var header=initial with{Cells=[initial.Cells[0]]};Assert.NotNull(tracker.Observe(header,200));Assert.Null(tracker.Observe(header,1000));
        tracker.Observe(initial,1100);Assert.Null(tracker.Observe(header with{Cells=[header.Cells[0] with{Source="Lucia"}]},1200));
    }
    [Fact]public async Task PhoneAuthorsStayInThePhoneAndDoNotLeakToDialogue()
    {
        long at=0;var reader=new SpatialReader("Sinfrontera\nAndrea: Blueprints and outfits\nGetaway vehicle is good.");
        reader.Source=new([new("Sinfrontera",new(.3,.04,.7,.08)),new("Andrea: Blueprints and outfits",new(.1,.22,.9,.28)),new("Getaway vehicle is good.",new(.1,.4,.9,.46))]);
        var area=ReadingArea.Phone() with{AutoDetect=false,Source=new(0,1,0,1),IntervalMs=16};using var session=new ReadingAreaSession([area],()=>reader,new SpeakerEngine(),()=>at);
        var frame=Light();Assert.True(PhoneReadingEvidence.VerifiedConversation(frame,reader.Source));await session.ProcessAsync(frame);
        var author=reader.Source.Lines[1];Assert.Empty(session.WithoutMessageText(new([author]),new(0,0,1,1)).Lines);
        Assert.Single(session.WithoutMessageText(new([new TextLine("Jason: Let's get moving.",author.Box)]),new(0,0,1,1)).Lines);
        Assert.False(PhoneReadingEvidence.VerifiedConversation(Frame.Blank(400,800),reader.Source));
    }
    [Fact]public void BubbleAndHeaderExpandHorizontallyBeforeTheConversationGrowsTall()
    {
        var p=Page("Sinfrontera","Got a few things to take care of right now, but will get searching later.") with{Cells=[new("Sinfrontera","سينفرونتيرا",new(.4,.02,.58,.06),"#F3F3F3","#20232A",.03,false,true),new("Got a few things to take care of right now, but will get searching later.","لدي بعض الأمور التي يجب أن أنهيها الآن، لكنني سأبدأ البحث لاحقًا.",new(.6,.2,.95,.32),"#287DE0","#FFFFFF",.035,true)]};
        var r=SpatialText.ReflowPhone(p,24);Assert.Equal(p.Width,r.Width);Assert.True(r.Cells[1].Box.Right-r.Cells[1].Box.Left>.6);Assert.Equal(p.Cells[1].Box.Right,r.Cells[1].Box.Right);Assert.True(r.Cells[0].Box.Right-r.Cells[0].Box.Left>.18);Assert.True(r.Height<1000);Assert.Equal(p.Cells[1].Background,r.Cells[1].Background);
    }
    [Fact]public void AFullWidthPhoneHeaderAndBubbleAreNeverNarrowedOrRejected()
    {
        var p=Page("Sinfrontera","Meet me at home later.") with{Cells=Page("Sinfrontera","Meet me at home later.").Cells.Select(c=>c with{Box=c.Box with{Left=0,Right=1}}).ToArray()};
        var result=SpatialText.ReflowPhone(p,24);Assert.All(result.Cells,c=>{Assert.Equal(0,c.Box.Left);Assert.Equal(1,c.Box.Right);});
    }
    [Fact]public void TheRecordedCoolGlyphRepairNeedsAUniqueKnownWordAndPreservesLabels()
    {
        var words=new Words("cool","dog","flight","was","fine");Assert.Equal("Jason: COOL dog.",OcrGlyphRepair.Repair("Jason: COOI dog.",words));Assert.Equal("Driver: Flight was fine.",OcrGlyphRepair.Repair("Driver: FIight was fine.",words));
        Assert.Equal("Nason: I will go.",OcrGlyphRepair.Repair("Nason: I will go.",words));Assert.Equal("Jason: FIB agents.",OcrGlyphRepair.Repair("Jason: FIB agents.",words));Assert.Equal("Jason: COOI dog.",OcrGlyphRepair.Repair("Jason: COOI dog.",new Words("cooi","cool")));Assert.Equal("COOI",OcrGlyphRepair.Repair("COOI",words));
    }
    [Fact]public async Task NewNotificationConfirmsAfterSettleInsteadOfTheFullBackgroundInterval()
    {
        long at=0;var reader=new SpatialReader("Andy\nI will meet you at the store.");var engine=new SpeakerEngine();var area=ReadingArea.Notification() with{AutoDetect=false,Source=new(0,1,0,1),IntervalMs=450};
        using var session=new ReadingAreaSession([area],()=>reader,engine,()=>at);session.SetTiming(new(SettleMs:120));
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);session.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};
        await session.ProcessAsync(Light());Assert.Empty(engine.Requested);at=120;await session.ProcessAsync(Light());await shown.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.NotEmpty(engine.Requested);
    }
    private static PanelLayout Page(string name,string text)=>new(400,800,"phone",null,[new(name,"",new(.3,.04,.7,.08),"#FFFFFF","#20232A",.03,false,true),new(text,"",new(.1,.2,.9,.35),"#FFFFFF","#20232A",.03,true)]);
    private static Frame Light(){var f=Frame.Blank(400,800);Array.Fill(f.Pixels,(byte)235);return f;}
    private sealed class SpatialReader(string source):ITextReader
    {
        public Reading Source=new(source.Split('\n').Select((s,i)=>new TextLine(s,new(.1,.03+i*.13,.9,.08+i*.13))));public string Read(Frame f)=>Source.Text;public Reading ReadSpatial(Frame f)=>Source;
    }
    private sealed class SpeakerEngine:ITextTranslator
    {
        public readonly List<string> Requested=[];public Task<string> TranslateAsync(string text,CancellationToken token=default){Requested.Add(text);return Task.FromResult(text.StartsWith("Nason")?"Nason: نعم، ربما في وقت آخر.":text.StartsWith("Jason")?"جيسون: نعم، ربما في وقت آخر.":"ترجمة عربية سليمة.");}
    }
    private sealed class MapEngine:ITextTranslator,IRejectedTranslationDiagnostics
    {
        public readonly List<string> Requested=[];public string? RejectedOutput(string source)=>source=="UNKNOWN VENUE"?"UNKNOWN VENUE":null;
        public Task<string> TranslateAsync(string text,CancellationToken token=default){Requested.Add(text);return text=="UNKNOWN VENUE"?Task.FromException<string>(new TranslatorException(TranslatorStatus.OutputRejected)):Task.FromResult(text=="FEATURES"?"الميزات":"مكان صغير يبيع الطعام والشراب.");}
    }
    private sealed class TurnsEngine:ITextTranslator{public Task<string> TranslateAsync(string source,CancellationToken token=default)=>Task.FromResult(source.StartsWith("Jason:")?"جيسون: كيف لا تفقده؟\nأحد السكان: انظر إليّ!":"أحد السكان: انظر إليّ!");}
    private sealed class Words(params string[] words):ISpellDictionary{public bool Contains(string word)=>words.Contains(word,StringComparer.OrdinalIgnoreCase);}
}
