using System.Net;
using GameTranslator.Core;
namespace GameTranslator.Translation;
public sealed record GeminiProbeResult(OnlineKeyState State,string? Sample,int? HttpStatus,int Attempts,string? TransportError);
public static class GeminiConnectionProbe
{
 private sealed class Tracking(HttpMessageHandler inner):DelegatingHandler(inner){
  public int? Status;public string? Error;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){
   Status=null;Error=null;
   try{var response=await base.SendAsync(request,token).ConfigureAwait(false);Status=(int)response.StatusCode;return response;}
   catch(HttpRequestException e){Error=e.HttpRequestError.ToString();throw;}
   catch(OperationCanceledException){Error="Timeout";throw;}
  }
 }
 public static async Task<GeminiProbeResult> CheckAsync(string key,string? model,HttpMessageHandler? transport=null,CancellationToken cancellationToken=default){
  using var tracking=new Tracking(transport??new HttpClientHandler());
  using var http=new HttpClient(tracking){Timeout=AiTranslator.CloudTestTimeout};
  using var budget=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);budget.CancelAfter(AiTranslator.CloudTestTimeout);
  var attempts=0;
  try{
   AiTestResult result;
   do{
    attempts++;result=await AiTranslator.TestAsync(AiProvider.Gemini,key,model,http,budget.Token).ConfigureAwait(false);
    if(tracking.Status!=503||attempts==2)break;
    await Task.Delay(400,budget.Token).ConfigureAwait(false);
   }while(true);
   return new(result.State,result.Sample,tracking.Status,attempts,tracking.Error);
  }catch(OperationCanceledException)when(!cancellationToken.IsCancellationRequested){return new(OnlineKeyState.Unreachable,null,tracking.Status,attempts,"Timeout");}
 }
}
