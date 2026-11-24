// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
namespace GameTranslator.Core.Tests;
public class TranslationIsolationTests
{
    private sealed class BlockingTranslator : ITextTranslator
    {
        public readonly ManualResetEventSlim Entered = new();
        public readonly ManualResetEventSlim Release = new();
        public Task<string> TranslateAsync(string text,CancellationToken cancellationToken=default)
        { Entered.Set(); Release.Wait(TimeSpan.FromSeconds(10),cancellationToken); return Task.FromResult("ترجمة عربية"); }
    }
    [Fact] public async Task SynchronousProviderCannotHoldFrameProcessingOrReset()
    {
        var translator=new BlockingTranslator();
        var pipeline=new SubtitlePipeline(new FakeReader("This is a complete sentence."),translator,new FakeSender());
        try
        {
            var frame=pipeline.ProcessAsync(Frame.Blank(160,90),new());
            await frame.WaitAsync(TimeSpan.FromSeconds(3));
            Assert.True(translator.Entered.Wait(TimeSpan.FromSeconds(3)));
            await Task.Run(pipeline.Reset).WaitAsync(TimeSpan.FromSeconds(3));
        }
        finally { translator.Release.Set(); await pipeline.WaitForTranslationsAsync(); }
    }
}
