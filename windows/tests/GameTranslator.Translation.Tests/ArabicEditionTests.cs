// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Text;
using System.Text.Json;
using GameTranslator.Translation;

namespace GameTranslator.Translation.Tests;

public class ArabicEditionTests
{
    private sealed class ArabicServer : HttpMessageHandler
    {
        public int Calls;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            Calls++;
            var body = await request.Content!.ReadAsStringAsync(cancellationToken);
            using var doc = JsonDocument.Parse(body);
            Assert.Equal("AR", doc.RootElement.GetProperty("target_lang").GetString());
            return new HttpResponseMessage(HttpStatusCode.OK)
            { Content = new StringContent("{\"translations\":[{\"text\":\"لدينا 25 دولارًا.\"}]}", Encoding.UTF8, "application/json") };
        }
    }
    [Fact]
    public async Task ArabicRoundTripPreservesSpeakerAndReusesCache()
    {
        var server = new ArabicServer();
        var translator = new DeepLTranslator("test:fx", null, new HttpClient(server));
        var first = await translator.TranslateAsync("ARTHUR: We have 25 dollars.");
        Assert.Contains("ARTHUR:", first);
        Assert.Contains("لدينا 25 دولارًا.", first);
        Assert.Equal(first, await translator.TranslateAsync("ARTHUR: We have 25 dollars."));
        Assert.Equal(1, server.Calls);
    }
    [Fact]
    public void AiPromptTargetsArabicAndKeepsRdrContext()
    {
        Assert.Contains("Modern Standard Arabic", AiRequest.SystemPrompt);
        Assert.DoesNotContain("Turkish", AiRequest.SystemPrompt);
        var message = AiRequest.UserMessage(["We need money."], [new AiContextLine("Dutch?", "داتش؟")], "Red Dead Redemption 2");
        Assert.Contains("Arabic strings", message);
        Assert.Contains("Red Dead Redemption 2", message);
        Assert.Contains("داتش؟", message);
    }
}
