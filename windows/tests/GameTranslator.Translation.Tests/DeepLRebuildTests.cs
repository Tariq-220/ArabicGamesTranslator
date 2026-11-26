// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Net.Http.Headers;
using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class DeepLRebuildTests
{
    private sealed class Service(Func<int,string,HttpResponseMessage> reply):HttpMessageHandler
    {
        public readonly List<string> Bodies=[];
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            var body=await request.Content!.ReadAsStringAsync(token);Bodies.Add(body);return reply(Bodies.Count,body);
        }
    }
    private static HttpResponseMessage Arabic(string text="تحرك الآن.")=>new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(new{translations=new[]{new{text}}}))};
    private static string Context(string body)=>JsonDocument.Parse(body).RootElement.GetProperty("context").GetString()!;
    [Fact]public async Task Transient503DoesNotDisableDeepLForAMinuteOrPolluteItsContext()
    {
        var now=DateTimeOffset.UtcNow;var service=new Service((call,_)=>call==1?new(HttpStatusCode.ServiceUnavailable):Arabic());
        var engine=new DeepLTranslator("test:fx",null,new HttpClient(service),game:"GTA6",validateArabic:true,clock:()=>now);
        await Assert.ThrowsAsync<TranslatorException>(()=>engine.TranslateAsync("Failed dialogue."));
        now=now.AddSeconds(1);await Assert.ThrowsAsync<TranslatorException>(()=>engine.TranslateAsync("Skipped during cooldown."));Assert.Single(service.Bodies);
        now=now.AddSeconds(2);Assert.Equal("تحرك الآن.",await engine.TranslateAsync("Move now."));Assert.Equal(2,service.Bodies.Count);
        Assert.DoesNotContain("Failed dialogue.",Context(service.Bodies[1]));Assert.DoesNotContain("Skipped during cooldown.",Context(service.Bodies[1]));
    }
    [Fact]public async Task RateLimitHonorsRetryAfterWithoutHammeringTheApi()
    {
        var now=DateTimeOffset.UtcNow;var service=new Service((call,_)=>{if(call>1)return Arabic();var response=new HttpResponseMessage(HttpStatusCode.TooManyRequests);response.Headers.RetryAfter=new RetryConditionHeaderValue(TimeSpan.FromSeconds(5));return response;});
        var engine=new DeepLTranslator("test:fx",null,new HttpClient(service),clock:()=>now);
        await Assert.ThrowsAsync<TranslatorException>(()=>engine.TranslateAsync("Wait."));now=now.AddSeconds(4);
        await Assert.ThrowsAsync<TranslatorException>(()=>engine.TranslateAsync("Hold on."));Assert.Single(service.Bodies);
        now=now.AddSeconds(1);await engine.TranslateAsync("Move now.");Assert.Equal(2,service.Bodies.Count);
    }
    [Fact]public async Task MenusDoNotEnterDialogueContextAndDialogueDoesNotEnterMenus()
    {
        var service=new Service((_,_)=>Arabic());var dialogue=new DeepLTranslator("test:fx",null,new HttpClient(service),game:"GTA6",validateArabic:true);
        await dialogue.TranslateAsync("Let's go.");var area=dialogue.ForIndependentArea();await area.TranslateAsync("Map pin unavailable");await dialogue.TranslateAsync("Stay close.");
        Assert.DoesNotContain("Let's go.",Context(service.Bodies[1]));Assert.DoesNotContain("Map pin unavailable",Context(service.Bodies[2]));Assert.Contains("Let's go.",Context(service.Bodies[2]));
    }
    [Fact]public async Task InvalidOutputIsNotRememberedOrPersisted()
    {
        var cache=new TranslationCache(targetLanguage:"ar");var service=new Service((call,_)=>Arabic(call==1?"Untranslated source":"ترجمة عربية صحيحة."));
        var engine=new DeepLTranslator("test:fx",null,new HttpClient(service),cache:cache,game:"GTA6",validateArabic:true);
        await engine.TranslateAsync("First source.");Assert.False(cache.TryGet("First source.",out _));
        await engine.TranslateAsync("Second source.");Assert.DoesNotContain("First source.",Context(service.Bodies[1]));
        Assert.Contains("Modern Standard Arabic",Context(service.Bodies[1]));
    }
    [Fact]public void ArabicCacheKeepsDialogueAndNormalizesSafeLexicalChanges(){var file=Path.GetTempFileName();try{File.WriteAllText(file,System.Text.Json.JsonSerializer.Serialize(new[]{"What do you want?","وش تبغى؟"})+"\n"+System.Text.Json.JsonSerializer.Serialize(new[]{"You son of a bitch!","يا ابن القحبة!"})+"\n");var cache=new TranslationCache(file,targetLanguage:"ar");Assert.True(cache.TryGet("What do you want?",out var intelligible));Assert.Equal("ماذا تريد؟",intelligible);Assert.True(cache.TryGet("You son of a bitch!",out var normalized));Assert.Equal("يا ابن العاهرة!",normalized);}finally{File.Delete(file);}}
    [Fact]public async Task ValidCacheAvoidsAnotherRequestAfterEngineRecreation()
    {
        var file=Path.Combine(Path.GetTempPath(),Guid.NewGuid()+".jsonl");
        try{
            var service=new Service((_,_)=>Arabic("افتح الباب."));using var http=new HttpClient(service);
            await new DeepLTranslator("test:fx",null,http,cache:new TranslationCache(file,targetLanguage:"ar"),validateArabic:true).TranslateAsync("Open the door.");
            var result=await new DeepLTranslator("test:fx",null,http,cache:new TranslationCache(file,targetLanguage:"ar"),validateArabic:true).TranslateAsync("Open the door.");
            Assert.Equal("افتح الباب.",result);Assert.Single(service.Bodies);
        }finally{File.Delete(file);}
    }
    [Fact]public void WrappedDialogueRetainsActualSpeakerBoundaries()=>Assert.Equal("Jason: Let's go.\nLucia: Stay close.",ArabicOutputQuality.DialogueInput("Jason: Let's\ngo.\nLucia: Stay close."));
    [Fact]public async Task RegionalMemoryUsesTheProviderOnceWithoutSuppressingTheLine(){var cache=new TranslationCache(targetLanguage:"ar");cache.Store("All this because of a video?","كل هذا عشان فيديو؟");var service=new Service((_,_)=>Arabic("كل هذا بسبب مقطع فيديو؟"));var engine=new ProfileTranslator(new DeepLTranslator("test:fx",null,new HttpClient(service),cache:cache,validateArabic:true),"GTA6");Assert.Equal("كل هذا بسبب مقطع فيديو؟",await engine.TranslateAsync("All this because of a video?"));Assert.Equal("كل هذا بسبب مقطع فيديو؟",await engine.TranslateAsync("All this because of a video?"));Assert.Single(service.Bodies);}
}
