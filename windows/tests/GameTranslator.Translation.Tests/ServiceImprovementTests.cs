using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ServiceImprovementTests
{
 private sealed class Replies(params HttpResponseMessage[] responses):HttpMessageHandler{
  public List<string> Bodies=[];public int Calls;
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){Calls++;Bodies.Add(request.Content==null?"":await request.Content.ReadAsStringAsync(token));return responses[Math.Min(Calls-1,responses.Length-1)];}
 }
 private static HttpResponseMessage Json(string value,HttpStatusCode status=HttpStatusCode.OK)=>new(status){Content=new StringContent(value,Encoding.UTF8,"application/json")};
 private const string GeminiOk="""{"candidates":[{"content":{"parts":[{"text":"[\"نذهب الآن.\",\"ابقَ قريبًا.\"]"}]}}]}""";
 [Fact]public async Task Gemini503RetriesOnceWithSameModelAndAcceptsRecovery(){
  var handler=new Replies(new(HttpStatusCode.ServiceUnavailable),Json(GeminiOk));
  var result=await GeminiConnectionProbe.CheckAsync("test","gemini-3.8-flash",handler);
  Assert.Equal(OnlineKeyState.Valid,result.State);Assert.Equal(2,result.Attempts);Assert.Equal(200,result.HttpStatus);Assert.Equal(2,handler.Calls);
 }
 [Fact]public async Task Persistent503IsNotReportedAsAnInvalidKey(){
  var handler=new Replies(new(HttpStatusCode.ServiceUnavailable),new(HttpStatusCode.ServiceUnavailable));
  var result=await GeminiConnectionProbe.CheckAsync("test","gemini-3.8-flash",handler);
  Assert.Equal(OnlineKeyState.Unreachable,result.State);Assert.Equal(503,result.HttpStatus);Assert.Equal(2,handler.Calls);
 }
 [Theory][InlineData(403,OnlineKeyState.Invalid)][InlineData(429,OnlineKeyState.QuotaExceeded)]
 public async Task BadKeyAndQuotaAreNotRetried(int status,OnlineKeyState expected){
  var handler=new Replies(Json("{}",(HttpStatusCode)status));var result=await GeminiConnectionProbe.CheckAsync("test","gemini-3.8-flash",handler);
  Assert.Equal(expected,result.State);Assert.Equal(1,handler.Calls);
 }
 [Theory][InlineData("latency_optimized")][InlineData("prefer_quality_optimized")]
 public async Task DeepLModeAndGameContextReachTheRequest(string mode){
  var handler=new Replies(Json("{\"translations\":[{\"text\":\"استمر.\",\"model_type_used\":\"quality_optimized\"}]}"));
  var translator=new DeepLTranslator("test",null,new HttpClient(handler),modelType:mode,game:"Red Dead Redemption 2");
  await translator.TranslateAsync("Keep going.");using var body=JsonDocument.Parse(handler.Bodies[0]);
  Assert.Equal(mode,body.RootElement.GetProperty("model_type").GetString());Assert.Contains("1899",body.RootElement.GetProperty("context").GetString());Assert.Equal("quality_optimized",translator.ActualModelType);
 }
 [Fact]public async Task DeepLUnsupportedModeDoesNotRejectOrEraseTheKey(){
  var handler=new Replies(Json("{}",HttpStatusCode.BadRequest));var translator=new DeepLTranslator("test",null,new HttpClient(handler),modelType:"latency_optimized");
  var error=await Assert.ThrowsAsync<TranslatorException>(()=>translator.TranslateAsync("Keep going."));Assert.Equal(TranslatorStatus.ModelRejected,error.Status);
 }
 [Theory][InlineData("Ready I { = SessionState { Phase = Streaming, Reason")][InlineData("- Streaming. Reason QI -")][InlineData("PHOTO MODE SELECT O BACK O")][InlineData("Comparison - All")]
 public void InterfaceTextDoesNotEnterDialogueContext(string source){Assert.True(DialogueProfile.IsInterfaceText(source));Assert.Empty(DialogueProfile.DialogueOnly(Reading.FromText(source),new(0,0,1,1),"RDR2").Lines);}
 [Theory][InlineData("Are you ready?")][InlineData("Give me a reason.")][InlineData("We should still have some decent views over the river.")]
 public void NormalDialogueIsNotMistakenForAppUi(string source)=>Assert.False(DialogueProfile.IsInterfaceText(source));
}
