using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class ReadingAreaTests_Recovery
{
    [Fact]public void MapPinMessagesRemainValidPhoneContentButStreetNamesDoNot(){Assert.True(SpatialText.IsConversation(new([new("Kenton",new(.3,.03,.6,.08)),new("Map Pin Unavailable",new(.1,.3,.9,.4))])));Assert.False(SpatialText.IsConversation(new([new("Little Village",new(.3,.03,.6,.08)),new("Moonstone Boutique",new(.1,.3,.9,.4))])));Assert.False(SpatialText.IsConversation(new([new("MAP BRIEF PROGRESS",new(.2,.03,.8,.08)),new("A mission description.",new(.1,.3,.9,.4))])));}
    private sealed class Reader:ITextReader{public string Read(Frame f)=>"";public Reading ReadSpatial(Frame f)=>new(Enumerable.Range(0,9).Select(i=>new TextLine("Message number "+i,new(.1,.05+i*.1,.8,.09+i*.1))));}
    private sealed class BatchEngine:ITextTranslator{
        public int PreferredBatchSize=>4;public int Calls;public TaskCompletionSource Second=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string s,CancellationToken t=default)=>Task.FromResult("رسالة.");
        public async Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> rows,CancellationToken t=default){if(Interlocked.Increment(ref Calls)==2){Second.TrySetResult();await Release.Task.WaitAsync(t);}return rows.Select(r=>"هذه رسالة "+r[^1]).ToArray();}
    }
    [Fact]public async Task FirstPhoneBatchIsVisibleWhileRemainingMessagesAreStillTranslating(){
        long now=0;var engine=new BatchEngine();using var session=new ReadingAreaSession([ReadingArea.Phone() with{AutoDetect=false,Source=new(0,1,0,1)}],()=>new Reader(),engine,()=>now);
        var first=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);session.Caption+=c=>first.TrySetResult(c);await session.ProcessAsync(Frame.Blank(300,800));now=500;await session.ProcessAsync(Frame.Blank(300,800));await engine.Second.Task.WaitAsync(TimeSpan.FromSeconds(3));var value=await first.Task.WaitAsync(TimeSpan.FromSeconds(3));Assert.Equal(4,value.Panel!.Cells.Count(c=>c.Text.Length>0));Assert.Equal("phone",value.Panel.Kind);engine.Release.TrySetResult();
    }
    [Fact]public async Task MapLabelsCannotProduceAWhitePhonePanel(){
        long now=0;var f=Frame.Blank(300,600);Array.Fill(f.Pixels,(byte)230);using var session=new ReadingAreaSession([ReadingArea.Phone() with{Source=new(0,1,0,1)}],()=>new MapReader(),new FakeTranslator(),()=>now);var captions=new List<AreaCaption>();session.Caption+=captions.Add;await session.ProcessAsync(f);now=500;await session.ProcessAsync(f);Assert.Empty(captions);
    }
    [Fact]public void PhoneReflowPreservesBubbleColoursAndPreventsAdjacentOverlap(){
        var p=new PanelLayout(300,600,"phone",null,[new("Yes!",new string('س',110),new(.5,.1,.95,.15),"#287DE0","#FFFFFF",.04,true),new("Hello.","مرحبًا",new(.05,.2,.8,.25),"#F3F3F3","#20232A",.04,true)]);var r=SpatialText.ReflowPhone(p);Assert.Equal("#287DE0",r.Cells[0].Background);Assert.Equal("#FFFFFF",r.Cells[0].Foreground);Assert.True(r.Cells[0].Box.Bottom<r.Cells[1].Box.Top);Assert.True(r.Cells[0].Box.Bottom-r.Cells[0].Box.Top>.05);
    }
    [Fact]public async Task MissionTextUsesReadyPackWithoutCallingEngine(){long now=0;using var session=new ReadingAreaSession([ReadingArea.Instructions()],()=>new PlainReader(),new MustNotTranslate(),()=>now,reviewedLookup:_=>"اتبع لوسيا.");var shown=new TaskCompletionSource<AreaCaption>(TaskCreationOptions.RunContinuationsAsynchronously);session.Caption+=c=>shown.TrySetResult(c);await session.ProcessAsync(Frame.Blank(400,200));now=500;await session.ProcessAsync(Frame.Blank(400,200));Assert.Equal("اتبع لوسيا.",(await shown.Task.WaitAsync(TimeSpan.FromSeconds(3))).Text);}
    private sealed class MapReader:ITextReader{public string Read(Frame f)=>"Unofficial";public Reading ReadSpatial(Frame f)=>new([new(Read(f),new(.7,.5,.9,.54))]);}
    private sealed class PlainReader:ITextReader{public string Read(Frame f)=>"Follow Lucia.";}
    private sealed class MustNotTranslate:ITextTranslator{public Task<string> TranslateAsync(string s,CancellationToken t=default)=>throw new Exception("Ready text must bypass the engine");}
}
