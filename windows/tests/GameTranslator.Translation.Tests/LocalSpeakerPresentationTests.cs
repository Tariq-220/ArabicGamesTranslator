// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Translation;
namespace GameTranslator.Translation.Tests;
public class LocalSpeakerPresentationTests
{
 [Theory][InlineData("Jason: Open the door.","Open the door.","جيسون: افتح الباب.")][InlineData("Tourist: Open the door.","Open the door.","السائح: افتح الباب.")][InlineData("Billy: Open the door.","Open the door.","بيلي: افتح الباب.")][InlineData("Stranger: Open the door.","Open the door.","Stranger: افتح الباب.")]
 public async Task LocalModelReceivesUtteranceWithoutSpeakerMetadata(string source,string expected,string result){var service=new Service();var engine=new LocalBridgeTranslator("quickmt","test",new HttpClient(service));Assert.Equal(result,await engine.TranslateAsync(source));Assert.Equal(expected,service.Source);}
 private sealed class Service:HttpMessageHandler{public string? Source;protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage r,CancellationToken t){using var j=JsonDocument.Parse(await r.Content!.ReadAsStringAsync(t));Source=j.RootElement.GetProperty("text")[0].GetString();return new(HttpStatusCode.OK){Content=new StringContent("{\"translations\":[{\"text\":\"افتح الباب.\"}]}",Encoding.UTF8,"application/json")};}}
}
