// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class ReadingAreaTests
{
    [Fact] public void BoundsStayInsideFrameIncludingInvalidSavedCoordinates()
    {
        var area=ReadingArea.Phone() with { Source=new(double.NaN,2,-1,double.PositiveInfinity),FontSize=500,DurationMs=1 };
        var fixedArea=area.Validate();Assert.InRange(fixedArea.Source.Left,0,.97);
        Assert.InRange(fixedArea.Source.Bottom,fixedArea.Source.Top+.03,1);
        Assert.InRange(fixedArea.Source.Right,fixedArea.Source.Left+.03,1);
        Assert.Equal(48,fixedArea.FontSize);Assert.Equal(1200,fixedArea.DurationMs);
    }
    [Fact] public async Task RemovedAreaCannotPublishSlowTranslation()
    {
        long now=0;var translator=new SlowTranslator();var results=new List<AreaCaption>();
        var session=new ReadingAreaSession([ReadingArea.Menu() with{AutoDetect=false}],()=>new FakeReader("A complete message.","A complete message."),translator,()=>now);
        session.Caption+=results.Add;
        await session.ProcessAsync(Frame.Blank(320,180));now=500;
        await session.ProcessAsync(Frame.Blank(320,180));
        await translator.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));session.Dispose();translator.Finish.TrySetResult("رسالة كاملة.");
        await Task.Delay(50);Assert.Empty(results);
    }
    private sealed class SlowTranslator:ITextTranslator
    {
        public readonly TaskCompletionSource Started=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource<string> Finish=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public Task<string> TranslateAsync(string text,CancellationToken token=default){Started.TrySetResult();return Finish.Task;}
    }
}
