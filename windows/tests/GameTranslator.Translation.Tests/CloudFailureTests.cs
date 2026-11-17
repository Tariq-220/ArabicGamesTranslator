using System.Net;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class CloudFailureTests
{
 private sealed class Failure(HttpStatusCode status) : HttpMessageHandler {
  protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token) => Task.FromResult(new HttpResponseMessage(status){Content=new StringContent("{\"error\":{\"message\":\"temporary test error\"}}")});
 }
 [Theory]
 [InlineData(HttpStatusCode.ServiceUnavailable,TranslatorStatus.ServiceUnreachable)]
 [InlineData(HttpStatusCode.Unauthorized,TranslatorStatus.KeyRejected)]
 [InlineData(HttpStatusCode.TooManyRequests,TranslatorStatus.QuotaExceeded)]
 public async Task CloudErrorsNeverAskForLocalModelDownload(HttpStatusCode status,TranslatorStatus expected){
  using var http=new HttpClient(new Failure(status));var ai=new AiTranslator(AiProvider.Gemini,"test-key",null,null,http,budget:new RequestBudget(int.MaxValue));
  var error=await Assert.ThrowsAsync<TranslatorException>(()=>ai.TranslateAsync("Keep going."));Assert.Equal(expected,error.Status);
  var repeated=await Assert.ThrowsAsync<TranslatorException>(()=>ai.TranslateAsync("Run!"));Assert.Equal(expected,repeated.Status);
 }
}
