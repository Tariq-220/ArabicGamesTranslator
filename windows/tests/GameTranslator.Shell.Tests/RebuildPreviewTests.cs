// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Concurrent;
using GameTranslator.Core;
using GameTranslator.Shell;
namespace GameTranslator.Shell.Tests;
public class RebuildPreviewTests
{
    [Fact]public void PreviewCoalescesFramesAndDoesNotStartTranslation()
    {
        var services=new FakeServices();var pending=new ConcurrentQueue<Action>();
        using var controller=new AppController(services,new MemoryStore(),new MemorySettings(),new FakeSleep(),Localizer.ForLanguage("en"),pending.Enqueue);
        var frameCount=0;controller.PropertyChanged+=(_,e)=>{if(e.PropertyName==nameof(AppController.LastFrame))frameCount++;};
        controller.StartSourcePreview();Assert.False(controller.IsRunning);Assert.True(services.Streams.Single().Started);
        Frame? last=null;for(var n=0;n<100;n++){last=Frame.Blank(16,9);services.Streams.Single().Emit(last);}
        while(pending.TryDequeue(out var action))action();
        Assert.Equal(1,frameCount);Assert.Same(last,controller.LastFrame);
        controller.StopSourcePreview();Assert.False(services.Streams.Single().Started);
    }
    [Fact]public void RegionMigrationRunsOnceAndKeepsKeysAndLaterEdits()
    {
        var store=new MemoryStore();store.Save("sentinel-api-key","retained");
        using var controller=AppControllerTests.Make(store:store);
        controller.PrepareRebuildAreas();Assert.Equal(new CropRegion(.80,.97,.10,.90),controller.Settings.Region);
        controller.Settings=controller.Settings with{Region=new(.4,.6,.2,.8)};
        controller.PrepareRebuildAreas();Assert.Equal(new CropRegion(.4,.6,.2,.8),controller.Settings.Region);Assert.Equal("retained",store.Load<string>("sentinel-api-key"));
    }

    [Fact]public void IndependentTimingSurvivesRestartWithoutChangingApiOrLayout(){var store=new MemoryStore();store.Save("sentinel-api-key","kept");var expected=new ReadingTiming(60,30,16,100,100,true,250);using(var controller=AppControllerTests.Make(store:store)){var before=controller.Settings.Region;controller.Timing=expected;Assert.Equal(before,controller.Settings.Region);}using(var restored=AppControllerTests.Make(store:store)){Assert.Equal(expected,restored.Timing);Assert.Equal("kept",store.Load<string>("sentinel-api-key"));}}
    [Fact]public void ExistingFasterPresetKeepsItsOriginalDisplayDelayOnTimingMigration(){var store=new MemoryStore();store.Save("FasterPresentation",true);store.Save("FastCaptureEnabled",true);using var controller=AppControllerTests.Make(store:store);Assert.Equal(80,controller.Timing.SettleMs);Assert.Equal(450,controller.Timing.MinimumDisplayMs);Assert.Equal(25,controller.Timing.CaptureFps);}
}
