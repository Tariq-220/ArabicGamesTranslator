using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class ReadingAreaTests_059
{
    private sealed class Messages : ITextReader
    {
        public bool Present=true;public Reading? Replacement;
        public string Read(Frame f)=>"";
        public Reading ReadSpatial(Frame f)=>!Present?Reading.Empty:Replacement??new([new("Lucia",new(.3,.02,.6,.06)),new("Meet me at home.",new(.1,.2,.9,.28)),new("A garbled unreadable row.",new(.1,.5,.9,.58))]);
    }
    private sealed class Partial : ITextTranslator
    {
        public Task<string> TranslateAsync(string text,CancellationToken token=default)=>Task.FromResult(text=="A garbled unreadable row."?"":text=="Lucia"?"لوسيا":"قابلني في المنزل.");
    }
    [Theory][InlineData(ReadingAreaKind.Phone)][InlineData(ReadingAreaKind.Notification)]
    public async Task ARejectedCellCannotHideOtherTranslatedMessages(ReadingAreaKind kind)
    {
        long now=0;var area=(kind==ReadingAreaKind.Phone?ReadingArea.Phone():ReadingArea.Notification()) with{AutoDetect=false,Source=new(.1,.9,.6,.95),DurationMs=1200};
        using var s=new ReadingAreaSession([area],()=>new Messages(),new Partial(),()=>now);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);s.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};
        await s.ProcessAsync(PhoneFrames.Light(500,500));now=500;await s.ProcessAsync(PhoneFrames.Light(500,500));
        var caption=await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.Contains("قابلني",caption.Text);Assert.NotNull(caption.Panel);
    }
    [Theory][InlineData(ReadingAreaKind.Phone)][InlineData(ReadingAreaKind.Notification)]
    public async Task PartialMessagesRemainVisibleWhileSourceIsConfirmed(ReadingAreaKind kind)
    {
        long now=0;var area=(kind==ReadingAreaKind.Phone?ReadingArea.Phone():ReadingArea.Notification()) with{AutoDetect=false,Source=new(.1,.9,.6,.95),DurationMs=1200};
        using var s=new ReadingAreaSession([area],()=>new Messages(),new Partial(),()=>now);
        var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var renewed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        s.Caption+=c=>{if(!c.Clear){if(now<1000)shown.TrySetResult();else renewed.TrySetResult();}};
        await s.ProcessAsync(PhoneFrames.Light(500,500));now=500;await s.ProcessAsync(PhoneFrames.Light(500,500));await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));
        now=1600;await s.ProcessAsync(PhoneFrames.Light(500,500));await renewed.Task.WaitAsync(TimeSpan.FromSeconds(2));
    }
    [Fact]public async Task DifferentOcrOfTheSamePhoneCellCannotLeakIntoDialogue()
    {
        long now=0;using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(.1,.9,.6,.95)}],()=>new Messages(),new Partial(),()=>now);
        await s.ProcessAsync(PhoneFrames.Light(500,500));
        var line=new TextLine("Meet rne at horne.",new(.64,.26,.91,.32));
        Assert.Empty(s.WithoutMessageText(new([line]),new(0,0,1,1)).Lines);
        Assert.Single(s.WithoutMessageText(new([line with{Box=new(.2,.7,.5,.76)}]),new(0,0,1,1)).Lines);
    }
    [Theory][InlineData(ReadingAreaKind.Phone)][InlineData(ReadingAreaKind.Notification)]
    public async Task MissingSourceClearsPartialMessagesInsteadOfPinningThemForever(ReadingAreaKind kind)
    {
        long now=0;var reader=new Messages();var area=(kind==ReadingAreaKind.Phone?ReadingArea.Phone():ReadingArea.Notification()) with{AutoDetect=false,Source=new(.1,.9,.6,.95)};
        using var s=new ReadingAreaSession([area],()=>reader,new Partial(),()=>now);var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var cleared=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        s.Caption+=c=>{if(c.Clear)cleared.TrySetResult();else shown.TrySetResult();};await s.ProcessAsync(PhoneFrames.Light(500,500));now=500;await s.ProcessAsync(PhoneFrames.Light(500,500));await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));
        reader.Present=false;now=1000;await s.ProcessAsync(PhoneFrames.Light(500,500));Assert.False(cleared.Task.IsCompleted);now=2000;await s.ProcessAsync(PhoneFrames.Light(500,500));await cleared.Task.WaitAsync(TimeSpan.FromSeconds(2));
        Assert.Single(s.WithoutMessageText(new([new TextLine("Meet me at home.",new(.64,.26,.91,.32))]),new(0,0,1,1)).Lines);
    }
    [Fact]public async Task MissingContactHeaderDoesNotClearAStillRecognizedPhoneMessage()
    {
        long now=0;var reader=new Messages();var frame=Frame.Blank(300,600);Array.Fill(frame.Pixels,(byte)230);
        using var s=new ReadingAreaSession([ReadingArea.Phone() with{Source=new(0,1,0,1),DurationMs=1200}],()=>reader,new Partial(),()=>now);
        var shown=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);var renewed=new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);int clears=0;
        s.Caption+=c=>{if(c.Clear)clears++;else if(now<1000)shown.TrySetResult();else renewed.TrySetResult();};
        await s.ProcessAsync(frame);now=500;await s.ProcessAsync(frame);await shown.Task.WaitAsync(TimeSpan.FromSeconds(2));
        reader.Replacement=new([new("Meet me at home.",new(.1,.2,.9,.28))]);now=1000;await s.ProcessAsync(frame);now=2000;await s.ProcessAsync(frame);await renewed.Task.WaitAsync(TimeSpan.FromSeconds(2));Assert.Equal(0,clears);
    }
    private sealed class RejectHeading : ITextTranslator
    {
        public Task<string> TranslateAsync(string text,CancellationToken token=default)=>text=="Lucia"?Task.FromException<string>(new TranslatorException(TranslatorStatus.OutputRejected)):Task.FromResult("رسالة عربية سليمة.");
    }
    [Fact]public async Task RejectedContactRequestCannotAbortTheWholeConversation()
    {
        long now=0;using var s=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(.1,.9,.6,.95)}],()=>new Messages(),new RejectHeading(),()=>now);
        var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);s.Caption+=c=>{if(!c.Clear)shown.TrySetResult(c);};
        await s.ProcessAsync(PhoneFrames.Light(500,500));now=500;await s.ProcessAsync(PhoneFrames.Light(500,500));
        Assert.Contains("رسالة عربية سليمة",(await shown.Task.WaitAsync(TimeSpan.FromSeconds(2))).Text);
    }
    private sealed class Blocking : ITextTranslator
    {
        public int PreferredBatchSize=>4;
        public readonly List<string> Calls=[];
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TranslateAsync(string text,CancellationToken token=default){lock(Calls)Calls.Add(text);if(text=="phone1"){Entered.TrySetResult();await Release.Task.WaitAsync(token);}return text;}
    }
    [Fact]public async Task DialogueRunsBetweenMessagesOfAnAreaBatch()
    {
        var engine=new Blocking();using var queue=new TranslationWorkQueue(engine);
        var page=queue.Areas.TranslateManyAsync(["phone1","phone2","phone3"]);await engine.Entered.Task.WaitAsync(TimeSpan.FromSeconds(2));
        var dialogue=queue.Dialogue.TranslateAsync("reply");engine.Release.TrySetResult();await Task.WhenAll(page,dialogue).WaitAsync(TimeSpan.FromSeconds(2));
        lock(engine.Calls)Assert.Equal(["phone1","reply","phone2","phone3"],engine.Calls);
    }
}
