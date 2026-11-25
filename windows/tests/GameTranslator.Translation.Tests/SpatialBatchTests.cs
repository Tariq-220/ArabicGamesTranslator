// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;
using GameTranslator.Translation;
namespace GameTranslator.Translation.Tests;
public class SpatialBatchTests
{
 [Fact]public async Task DeepLTranslatesSeparateBubblesInOneRequestAndRemembersOnlyValidPieces(){
  var service=new Service();ITextTranslator engine=new DeepLTranslator("test:fx",null,new HttpClient(service),game:"GTA6",rememberDialogue:false,validateArabic:true);
  var result=await engine.TranslateManyAsync(["Open the door.","Stay close."]);Assert.Equal(2,result.Count);Assert.Single(service.Bodies);using(var j=JsonDocument.Parse(service.Bodies[0])){Assert.Equal(2,j.RootElement.GetProperty("text").GetArrayLength());Assert.DoesNotContain("Previous dialogue:",j.RootElement.GetProperty("context").GetString());}
  await engine.TranslateManyAsync(["Open the door.","Stay close."]);Assert.Single(service.Bodies);
 }
 [Fact]public async Task LocalBatchKeepsIndividualSpeakerLabels(){var service=new Service(local:true);var engine=new LocalBridgeTranslator("quickmt","test",new HttpClient(service));var result=await engine.TranslateManyAsync(["Jason: Open the door.","Lucia: Stay close."]);Assert.Single(service.Bodies);Assert.StartsWith("جيسون:",result[0]);Assert.StartsWith("لوسيا:",result[1]);Assert.DoesNotContain("Jason:",service.Bodies[0]);}
 [Theory][InlineData(401,"",TranslatorStatus.KeyRejected)][InlineData(403,"",TranslatorStatus.KeyRejected)][InlineData(503,"",TranslatorStatus.ServiceBusy)][InlineData(400,"model_mismatch",TranslatorStatus.ModelRejected)][InlineData(500,"",TranslatorStatus.EngineFailed)]
 public async Task LocalFailuresKeepTheirActualCause(int code,string body,TranslatorStatus expected){using var http=new HttpClient(new Reply((_,_)=>Task.FromResult(new HttpResponseMessage((HttpStatusCode)code){Content=new StringContent(body)})));var error=await Assert.ThrowsAsync<TranslatorException>(()=>new LocalBridgeTranslator("quickmt","test",http).TranslateAsync("Move now."));Assert.Equal(expected,error.Status);}
 [Fact]public async Task OneRejectedLocalMessageDoesNotDiscardTheOtherMessages(){using var http=new HttpClient(new Reply((_,_)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StringContent("{\"translations\":[{\"text\":\"\"},{\"text\":\"افتح الباب.\"}]}")})));var engine=new LocalBridgeTranslator("quickmt","test",http);Assert.Equal(1,engine.PreferredBatchSize);var values=await engine.TranslateManyAsync(["Jason: Stay close.","Lucia: Open the door."]);Assert.Equal("",values[0]);Assert.Equal("لوسيا: افتح الباب.",values[1]);}
 [Fact]public async Task LocalNetworkLossIsDifferentFromUserCancellation(){using var offline=new HttpClient(new Reply((_,_)=>throw new HttpRequestException("offline")));var error=await Assert.ThrowsAsync<TranslatorException>(()=>new LocalBridgeTranslator("quickmt","test",offline).TranslateAsync("Move now."));Assert.Equal(TranslatorStatus.ServiceUnreachable,error.Status);using var stop=new CancellationTokenSource();stop.Cancel();using var cancelled=new HttpClient(new Reply((_,ct)=>Task.FromCanceled<HttpResponseMessage>(ct)));await Assert.ThrowsAnyAsync<OperationCanceledException>(()=>new LocalBridgeTranslator("quickmt","test",cancelled).TranslateAsync("Move now.",stop.Token));}
 private sealed class Reply(Func<HttpRequestMessage,CancellationToken,Task<HttpResponseMessage>> reply):HttpMessageHandler{protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)=>reply(request,token);}
 private sealed class Service(bool local=false):HttpMessageHandler{
  public readonly List<string> Bodies=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var body=await request.Content!.ReadAsStringAsync(token);Bodies.Add(body);using var j=JsonDocument.Parse(body);int n=j.RootElement.GetProperty("text").GetArrayLength();var data=new{translations=Enumerable.Range(0,n).Select(_=>new{text="افتح الباب."}).ToArray()};return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")};}
 }
}
