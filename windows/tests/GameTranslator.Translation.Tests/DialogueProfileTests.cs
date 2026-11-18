// SPDX-License-Identifier: AGPL-3.0-only
using GameTranslator.Core;
using GameTranslator.Translation;
using System.Text.Json;
namespace GameTranslator.Translation.Tests;
public class DialogueProfileTests
{
    private sealed class BridgeHandler(string expectedModel="quickmt") : HttpMessageHandler
    {
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken token)
        {
            Assert.Equal("127.0.0.1",request.RequestUri!.Host);Assert.Equal("Bearer local-test",request.Headers.Authorization!.ToString());
            var body=await request.Content!.ReadAsStringAsync(token);Assert.Contains(expectedModel,body);Assert.DoesNotContain("local-test",body);
            return new HttpResponseMessage(System.Net.HttpStatusCode.OK){Content=new StringContent("{\"translations\":[{\"text\":\"واصل التقدم.\"}]}")};
        }
    }
    [Fact]
    public async Task LocalModelCanTranslateWithoutAnyCloudKey()
    {
        using var client=new HttpClient(new BridgeHandler());var bridge=new LocalBridgeTranslator("quickmt","local-test",client);
        Assert.Equal("واصل التقدم.",await bridge.TranslateAsync("Keep going."));
    }
    [Theory]
    [InlineData("hy-mt")]
    [InlineData("jisr-msa")]
    [InlineData("jisr-saudi")]
    public async Task SelectedLocalModelIsPreservedInBridgeRequest(string model)
    {
        using var client=new HttpClient(new BridgeHandler(model));
        Assert.Equal("واصل التقدم.",await new LocalBridgeTranslator(model,"local-test",client).TranslateAsync("Keep going."));
    }
    [Fact]
    public async Task DesktopSecondScreenServesSafeTextViewer()
    {
        using var server=new SubtitleServer(0);var port=await server.StartAsync();using var client=new HttpClient();
        var page=await client.GetStringAsync($"http://127.0.0.1:{port}/screen");
        Assert.Contains("background:#0b111c",page);Assert.Contains("line.textContent",page);Assert.Contains("/events?viewer=obs",page);
    }
    [Fact]
    public async Task DefaultGeminiUsesLowThinkingAndArabicContext()
    {
        var model=AiProvider.Gemini.DefaultModel();
        Assert.Equal("gemini-3.8-flash",model.Id);
        using var request=AiRequest.Build(AiProvider.Gemini,"test-key",model,["Through the palms."],[new AiContextLine("Keep rowing.","واصل التجديف.")],"Red Dead Redemption 2");
        var text=await request.Content!.ReadAsStringAsync();using var json=JsonDocument.Parse(text);
        Assert.Equal("low",json.RootElement.GetProperty("generationConfig").GetProperty("thinkingConfig").GetProperty("thinkingLevel").GetString());
        Assert.DoesNotContain("test-key",text);Assert.Contains("Red Dead Redemption 2",text);Assert.Contains("Arabic",text);
    }
 [Fact]public void KnownCombatButtonsCannotBecomeDialogue(){var input=new Reading(new[]{new TextLine("Press X to fight",null),new TextLine("Get down!",null),new TextLine("PHOTO STUDIO X SELECT",null)});var output=DialogueProfile.DialogueOnly(input,new GameTranslator.Core.TextBox(0,0,1,1),"GTA6");Assert.Equal("Get down!",output.Text);}
}
