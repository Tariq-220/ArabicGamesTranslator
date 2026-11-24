// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class TranslationWorkQueueTests
{
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
