using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation.Tests;
public class ModelCompatibilityTests
{
 [Fact] public async Task OlderOpenAiModelDoesNotReceiveReasoningParameters(){
  using var request=AiRequest.Build(AiProvider.OpenAi,"test",new AiModel("gpt-4o-mini","legacy","none"),["Hello"],[]);
  using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
  Assert.False(body.RootElement.TryGetProperty("reasoning_effort",out _));
  Assert.Equal("system",body.RootElement.GetProperty("messages")[0].GetProperty("role").GetString());
 }
 [Fact] public async Task OlderClaudeModelDoesNotReceiveNewThinkingParameters(){
  using var request=AiRequest.Build(AiProvider.Claude,"test",new AiModel("claude-3-5-haiku-20241022","legacy","low","disabled"),["Hello"],[]);
  using var body=JsonDocument.Parse(await request.Content!.ReadAsStringAsync());
  Assert.False(body.RootElement.TryGetProperty("output_config",out _));Assert.False(body.RootElement.TryGetProperty("thinking",out _));
 }
}
