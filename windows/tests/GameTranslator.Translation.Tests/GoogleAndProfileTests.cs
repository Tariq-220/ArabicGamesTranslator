using System.Net;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class GoogleAndProfileTests {
 private sealed class Stub(string json,HttpStatusCode status=HttpStatusCode.OK):HttpMessageHandler {
  public int Calls;
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;Assert.Null(request.Headers.Authorization);Assert.Contains("tl=ar",request.RequestUri!.Query);return Task.FromResult(new HttpResponseMessage(status){Content=new StringContent(json)});}
 }
 [Theory][InlineData("[[[\"مرحبًا\",\"Hello\",null,null]]]", "Hello unique array")][InlineData("[\"مرحبًا\"]", "Hello unique flat")]
 public async Task GoogleWorksWithoutApiKeyAndUsesArabic(string response,string input){var handler=new Stub(response);using var client=new HttpClient(handler);Assert.Equal("مرحبًا",await new GoogleGtxTranslator(client:client).TranslateAsync(input));Assert.Equal(1,handler.Calls);}
 [Fact]public async Task GoogleStopsAtRateLimitInsteadOfTryingOtherEndpoints(){var handler=new Stub("{}",HttpStatusCode.TooManyRequests);using var client=new HttpClient(handler);var error=await Assert.ThrowsAsync<TranslatorException>(()=>new GoogleGtxTranslator(client:client).TranslateAsync("Unique quota sample"));Assert.Equal(TranslatorStatus.QuotaExceeded,error.Status);Assert.Equal(1,handler.Calls);}
 [Fact]public void Rdr2FixesAreScopedAndPreserveRealMeaningChanges(){Assert.Equal("I did.",DialogueProfile.English("1 did.","RDR2"));Assert.Equal("1 did.",DialogueProfile.English("1 did.","Another game"));Assert.Equal("I'm coming with you.",DialogueProfile.English("rm coming with you.","RDR2"));Assert.Equal("هيا، داتش",DialogueProfile.Arabic("Come on, Dutch","هيا، الهولندية","RDR2"));Assert.Equal("الهولندية",DialogueProfile.Arabic("Dutch","الهولندية","Another game"));Assert.Equal("We lost.",DialogueProfile.English("We lost.","RDR2"));Assert.Equal("We won.",DialogueProfile.English("We won.","RDR2"));}
 [Fact]public void WrappedSentenceIsJoinedBeforeTranslation(){Assert.Equal("In no time.",GoogleGtxTranslator.JoinWrappedLines("In no\ntime."));}
}
