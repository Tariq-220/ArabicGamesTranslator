using System.Net;
namespace GameTranslator.Core.Tests;
public class LocalRequestPolicyTests
{
 [Theory]
 [InlineData("https://attacker.example")]
 [InlineData("https://localhost.attacker.example")]
 [InlineData("http://8.8.8.8")]
 public void RejectsPublicWebOrigins(string origin)=>Assert.False(LocalRequestPolicy.Allows(IPAddress.Loopback,$"GET /events HTTP/1.1\r\nHost: 127.0.0.1:8787\r\nOrigin: {origin}"));
 [Theory]
 [InlineData("null")]
 [InlineData("file://")]
 [InlineData("http://192.168.0.118:8787")]
 public void PermitsTvAndPrivateSecondScreenOrigins(string origin)=>Assert.True(LocalRequestPolicy.Allows(IPAddress.Parse("192.168.0.50"),$"GET /events HTTP/1.1\r\nHost: 192.168.0.118:8787\r\nOrigin: {origin}"));
 [Fact] public void RejectsDnsRebindingHostAndPublicPeer(){
  Assert.False(LocalRequestPolicy.Allows(IPAddress.Loopback,"GET /hello HTTP/1.1\r\nHost: attacker.example:8787"));
  Assert.False(LocalRequestPolicy.Allows(IPAddress.Parse("8.8.8.8"),"GET /hello HTTP/1.1\r\nHost: 192.168.0.118:8787"));
 }
 [Fact] public async Task PublicOriginCannotReadLiveSubtitleStream(){
  using var server=new SubtitleServer(0);int port=await server.StartAsync();
  using var http=new HttpClient{Timeout=TimeSpan.FromSeconds(5)};
  using var request=new HttpRequestMessage(HttpMethod.Get,$"http://127.0.0.1:{port}/events");request.Headers.Add("Origin","https://attacker.example");
  using var response=await http.SendAsync(request);Assert.Equal(HttpStatusCode.Forbidden,response.StatusCode);Assert.Equal(0,server.ConnectedClients);
 }
}
