using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;

namespace GameTranslator.Translation.Tests;
public class ArabicDesktopUpdateTests
{
    private sealed class Handler(HttpStatusCode status, string json) : HttpMessageHandler
    {
        public string Body="", Url="", Region="", Credential="";public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken token)
        {
            Calls++;Body=await request.Content!.ReadAsStringAsync(token);Url=request.RequestUri!.ToString();
            Credential=request.Headers.GetValues("Ocp-Apim-Subscription-Key").Single();
            Region=request.Headers.TryGetValues("Ocp-Apim-Subscription-Region",out var values)?values.Single():"";
            return new(status){Content=new StringContent(json,Encoding.UTF8,"application/json")};
        }
    }
    [Fact] public async Task AzureKeepsSpeakerLabelBatchesSentencesAndReusesOwnCache()
    {
        var h=new Handler(HttpStatusCode.OK,"""[{"translations":[{"text":"تابع التقدم.","to":"ar"}]},{"translations":[{"text":"عبر النخيل.","to":"ar"}]}]""");
        var t=new AzureTranslator("test-secret","EastUS",new HttpClient(h));
        var result=await t.TranslateAsync("ARTHUR: Keep going. Through the palms.");
        Assert.Equal("ARTHUR: تابع التقدم. عبر النخيل.",result);
        Assert.Equal(result,await t.TranslateAsync("ARTHUR: Keep going. Through the palms."));Assert.Equal(1,h.Calls);
        Assert.Equal("eastus",h.Region);Assert.Equal("test-secret",h.Credential);Assert.DoesNotContain("test-secret",h.Url);
        Assert.Contains("from=en&to=ar",h.Url);using var body=JsonDocument.Parse(h.Body);Assert.Equal(2,body.RootElement.GetArrayLength());
        Assert.DoesNotContain("ARTHUR:",h.Body);
    }
    [Theory][InlineData(401,TranslatorStatus.KeyRejected)][InlineData(403,TranslatorStatus.KeyRejected)][InlineData(429,TranslatorStatus.QuotaExceeded)][InlineData(503,TranslatorStatus.ServiceUnreachable)]
    public async Task AzureErrorIsVisibleWithoutRetryOrAnotherEngine(int code,TranslatorStatus expected)
    {
        var h=new Handler((HttpStatusCode)code,"{\"error\":\"private response details\"}");
        var error=await Assert.ThrowsAsync<TranslatorException>(()=>new AzureTranslator("secret",http:new HttpClient(h)).TranslateAsync("Keep going."));
        Assert.Equal(expected,error.Status);Assert.Equal(1,h.Calls);Assert.DoesNotContain("private",error.Message);
    }
    [Fact] public async Task MalformedSuccessDoesNotBecomeCachedOrShownAsArabic()
    {
        var h=new Handler(HttpStatusCode.OK,"[]");var t=new AzureTranslator("secret",http:new HttpClient(h));
        var error=await Assert.ThrowsAsync<TranslatorException>(()=>t.TranslateAsync("Keep going."));Assert.Equal(TranslatorStatus.EngineFailed,error.Status);
        await Assert.ThrowsAsync<TranslatorException>(()=>t.TranslateAsync("Keep going."));Assert.Equal(2,h.Calls);
    }
    [Fact]public void RejectsHeaderInjectionBeforeNetwork()=>Assert.Throws<ArgumentException>(()=>new AzureTranslator("secret","eastus\r\nHeader: value"));
    [Fact]public void HistoricalContextIsScopedToTheSelectedGame()
    {
        Assert.Contains("1899",AiRequest.UserMessage(["Use the burner."],[],"RDR2"));
        Assert.DoesNotContain("1899",AiRequest.UserMessage(["Use the burner."],[],"Grand Theft Auto VI"));
    }
}
