// SPDX-License-Identifier: AGPL-3.0-only
using System.Collections.Concurrent;
using GameTranslator.Core;
using GameTranslator.Shell;
namespace GameTranslator.Shell.Tests;
public class RebuildPreviewTests
{
    [Theory][InlineData(false)][InlineData(true)]
    public async Task SpatialOcrReachesVisiblePhoneAndMissionThroughTheActualLocalOrDeepLQueue(bool deepL)
    {
        var services=new FakeServices{ReaderFactory=()=>new IndependentRegions()};
        using var controller=AppControllerTests.Make(services);controller.Settings=controller.Settings with{VideoSource=GameTranslator.Shell.VideoSourceType.DesktopScreen,SourceLanguage="en",TargetLanguage="ar"};
        using var http=new HttpClient(new ArabicService());controller.SessionTranslator=deepL?new GameTranslator.Translation.DeepLTranslator("test:fx",null,http,validateArabic:true):new GameTranslator.Translation.LocalBridgeTranslator("quickmt","test",http);
        var phone=ReadingArea.Phone() with{AutoDetect=false,Source=new(.1,.9,.6,.95)};var mission=ReadingArea.Instructions();controller.ReadingAreas=[phone,mission];controller.Timing=new(60,10,16,0,100);
        var visible=new System.Collections.Concurrent.ConcurrentDictionary<string,AreaCaption>();var traces=new System.Collections.Concurrent.ConcurrentBag<AreaTrace>();controller.AreaCaptionShown+=c=>{if(!c.Clear)visible[c.Id]=c;};controller.AreaTraceObserved+=traces.Add;
        await controller.StartAsync();for(int n=0;n<50&&visible.Count<2;n++){services.Streams.Single().Emit(Frame.Blank(300,600));await Task.Delay(40);}
        Assert.True(visible.ContainsKey(phone.Id));Assert.True(visible.ContainsKey(mission.Id));Assert.Equal("phone",visible[phone.Id].Panel!.Kind);Assert.Contains("اتبع",visible[mission.Id].Text);Assert.DoesNotContain(traces,t=>t.AreaId==mission.Id&&t.Source.Contains("Hello from home"));Assert.Contains(traces,t=>t.Stage=="shown");controller.Stop();
    }
    private sealed class IndependentRegions:ITextReader
    {
        public string Read(Frame f)=>ReadSpatial(f).Text;
        public Reading ReadLines(Frame f,bool deep)=>ReadSpatial(f);
        public Reading ReadSpatial(Frame f)=>f.Height>f.Width?new([new("Jason",new(.35,.04,.65,.08)),new("Hello from home.",new(.1,.2,.8,.3))]):f.Height<f.Width*.3?new([new("Follow the marked road.",new(.1,.1,.9,.8))]):Reading.Empty;
    }
    private sealed class ArabicService:System.Net.Http.HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){using var doc=System.Text.Json.JsonDocument.Parse(await request.Content!.ReadAsStringAsync(token));var inputs=doc.RootElement.GetProperty("text").EnumerateArray().Select(x=>x.GetString()).ToArray();return new(System.Net.HttpStatusCode.OK){Content=new StringContent(System.Text.Json.JsonSerializer.Serialize(new{translations=inputs.Select(x=>new{text=x=="Follow the marked road."?"اتبع الطريق المحدد.":"مرحبًا من المنزل."})}))};}
    }
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
    [Fact]public void SpeedExperimentRestoresTheUsersExactTimingAfterRestart(){var store=new MemoryStore();store.Save("sentinel-api-key","kept");var original=new ReadingTiming(12,30,250,180,500,true,400);using(var controller=AppControllerTests.Make(store:store)){controller.Timing=original;var area=controller.Settings.Region;controller.SpeedBoostEnabled=true;Assert.Equal(40,controller.Timing.SettleMs);Assert.Equal(20,controller.Timing.CaptureFps);Assert.Equal(150,controller.Timing.AreaReadMs);Assert.Equal(original.GoneMs,controller.Timing.GoneMs);Assert.Equal(area,controller.Settings.Region);}using(var restored=AppControllerTests.Make(store:store)){Assert.True(restored.SpeedBoostEnabled);restored.SpeedBoostEnabled=false;Assert.Equal(original,restored.Timing);Assert.Equal("kept",store.Load<string>("sentinel-api-key"));}}
 [Fact]public void MessageFontMigrationPreservesGeometryAndCustomFontsAndRunsOnce(){var store=new MemoryStore();using var controller=AppControllerTests.Make(store:store);var phone=ReadingArea.Phone() with{Source=new(.1,.9,.4,.8),Appearance=new(Font:"noto"),FontSize=22};var notice=ReadingArea.Notification() with{Appearance=new(Font:"cairo")};controller.ReadingAreas=[phone,notice];controller.PrepareRebuildAreas();var changed=controller.ReadingAreas.Single(a=>a.Id==phone.Id);Assert.Equal(phone.Source,changed.Source);Assert.Equal(phone.Output,changed.Output);Assert.Equal("tajawal",changed.Appearance!.Font);Assert.Equal(28,changed.FontSize);Assert.Equal("cairo",controller.ReadingAreas.Single(a=>a.Id==notice.Id).Appearance!.Font);controller.ReadingAreas=[changed with{Appearance=new(Font:"noto")},notice];controller.PrepareRebuildAreas();Assert.Equal("noto",controller.ReadingAreas.Single(a=>a.Id==phone.Id).Appearance!.Font);}
 [Fact]public void DialogueSizeAndMenuIsolationMigrationRunsOnceAndKeepsPositions(){var store=new MemoryStore();using var c=AppControllerTests.Make(store:store);var menu=ReadingArea.Menu();var map=ReadingArea.MapCard();var phone=ReadingArea.Phone();c.ReadingAreas=[menu,map,phone];c.PrepareRebuildAreas();Assert.False(c.Settings.Style.Appearance!.MatchSize);Assert.Equal(phone.Source,c.ReadingAreas.Single(a=>a.Id==phone.Id).Source);Assert.True(c.ReadingAreas.Single(a=>a.Id==phone.Id).Enabled);Assert.All(c.ReadingAreas.Where(a=>a.Kind is ReadingAreaKind.Menu or ReadingAreaKind.MapCard),a=>Assert.False(a.Enabled));c.Settings=c.Settings with{Style=c.Settings.Style with{Appearance=c.Settings.Style.Appearance with{MatchSize=true}}};c.PrepareRebuildAreas();Assert.True(c.Settings.Style.Appearance!.MatchSize);}
}
