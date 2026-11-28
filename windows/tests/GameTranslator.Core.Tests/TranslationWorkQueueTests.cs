// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class TranslationWorkQueueTests
{
    private sealed class CloudProvider(Provider areas):ITextTranslator
    {
        public bool SupportsConcurrentAreas=>true;
        public ITextTranslator ForIndependentArea()=>areas;
        public Task<string> TranslateAsync(string text,CancellationToken token=default)=>Task.FromResult("حوار "+text);
    }
    [Fact] public async Task IndependentCloudDialogueDoesNotWaitForActivePhoneRequest()
    {
        var areas=new Provider();using var queue=new TranslationWorkQueue(new CloudProvider(areas));
        var phone=queue.Areas.TranslateAsync("menu1");
        await areas.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        try
        {
            var nextPhone=queue.Areas.TranslateAsync("menu2");
            Assert.Equal("حوار reply",await queue.Dialogue.TranslateAsync("reply").WaitAsync(TimeSpan.FromSeconds(3)));
            Assert.False(phone.IsCompleted);Assert.False(nextPhone.IsCompleted);
            areas.Release.TrySetResult();
            await Task.WhenAll(phone,nextPhone).WaitAsync(TimeSpan.FromSeconds(3));
            Assert.Equal(["menu1","menu2"],areas.Called);
        }
        finally {areas.Release.TrySetResult();}
    }
    [Fact] public async Task DisposeCancelsBothCloudLanesAndPendingAreaRequests()
    {
        var areas=new Provider();var queue=new TranslationWorkQueue(new CloudProvider(areas));
        var phone=queue.Areas.TranslateAsync("menu1");
        await areas.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        var pending=queue.Areas.TranslateAsync("menu2");queue.Dispose();
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>phone.WaitAsync(TimeSpan.FromSeconds(3)));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>pending.WaitAsync(TimeSpan.FromSeconds(3)));
    }
    [Fact] public async Task ProbeSeparatesProviderWorkFromQueueWaiting()
    {
        long clock=0;var provider=new TimedProvider(()=>Interlocked.Add(ref clock,300));
        using var queue=new TranslationWorkQueue(provider,()=>Interlocked.Read(ref clock));
        var blocked=queue.Areas.TranslateAsync("menu");await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        Interlocked.Exchange(ref clock,100);var probe=new TranslationProbe();TranslationProbe.Current=probe;
        var dialogue=queue.Dialogue.TranslateAsync("reply");TranslationProbe.Current=null;
        Interlocked.Exchange(ref clock,500);provider.Release.TrySetResult();
        await dialogue.WaitAsync(TimeSpan.FromSeconds(3));await blocked;
        Assert.Equal(400,probe.QueueWaitMs);Assert.Equal(300,probe.ProviderMs);
    }
    private sealed class TimedProvider(Action translate):ITextTranslator
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously),Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public async Task<string> TranslateAsync(string text,CancellationToken token=default){if(text=="menu"){Entered.TrySetResult();await Release.Task.WaitAsync(token);}else translate();return "ترجمة";}
    }
    private sealed class Provider:ITextTranslator
    {
        public readonly TaskCompletionSource Entered=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly TaskCompletionSource Release=new(TaskCreationOptions.RunContinuationsAsynchronously);
        public readonly List<string> Called=[];
        public async Task<string> TranslateAsync(string text,CancellationToken token=default)
        {Called.Add(text);if(text=="menu1"){Entered.TrySetResult();await Release.Task.WaitAsync(token);}return "ترجمة "+text;}
    }
    [Fact] public async Task DialogueIsNextAndCancelledMenuDoesNotConsumeInference()
    {
        var provider=new Provider();using var queue=new TranslationWorkQueue(provider);
        var first=queue.Areas.TranslateAsync("menu1");await provider.Entered.Task.WaitAsync(TimeSpan.FromSeconds(3));
        using var cancelled=new CancellationTokenSource();var stale=queue.Areas.TranslateAsync("stale",cancelled.Token);cancelled.Cancel();
        var second=queue.Areas.TranslateAsync("menu2");var dialogue=queue.Dialogue.TranslateAsync("dialogue");provider.Release.TrySetResult();
        await Task.WhenAll(first,second,dialogue).WaitAsync(TimeSpan.FromSeconds(3));
        await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>stale);Assert.Equal(["menu1","dialogue","menu2"],provider.Called);
    }
}
