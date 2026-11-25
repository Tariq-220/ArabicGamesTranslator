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
 private sealed class Service(bool local=false):HttpMessageHandler{
  public readonly List<string> Bodies=[];
  protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token){var body=await request.Content!.ReadAsStringAsync(token);Bodies.Add(body);using var j=JsonDocument.Parse(body);int n=j.RootElement.GetProperty("text").GetArrayLength();var data=new{translations=Enumerable.Range(0,n).Select(_=>new{text="افتح الباب."}).ToArray()};return new(HttpStatusCode.OK){Content=new StringContent(JsonSerializer.Serialize(data),Encoding.UTF8,"application/json")};}
 }
}
