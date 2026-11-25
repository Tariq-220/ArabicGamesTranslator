// SPDX-License-Identifier: AGPL-3.0-only
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using GameTranslator.Core;
namespace GameTranslator.Translation;
public sealed class LocalBridgeTranslator(string model, string token, HttpClient? http=null) : ITextTranslator
{
    private static readonly HttpClient Client=new(){Timeout=TimeSpan.FromSeconds(5)};
    private static readonly HttpClient HyClient=new(){Timeout=TimeSpan.FromSeconds(20)};
    public async Task<string> TranslateAsync(string text,CancellationToken cancellationToken=default)
    {
        // Translation models are trained on utterances, not speaker metadata. Keep known labels outside inference.
        var (label,body)=CaptionAppearance.SplitSpeaker(text);var name=label.Length>0?ArabicGameNames.Speaker(label.TrimEnd(':','：')):null;
        if(name is not null)text=body;
        if(string.IsNullOrWhiteSpace(text))throw new TranslatorException(TranslatorStatus.EngineFailed);
        using var request=new HttpRequestMessage(HttpMethod.Post,"http://127.0.0.1:8790/translate");
        request.Headers.Authorization=new AuthenticationHeaderValue("Bearer",token);
        request.Content=new StringContent(JsonSerializer.Serialize(new {text=new[]{text},source_lang="EN",target_lang="AR",model}),Encoding.UTF8,"application/json");
        using var response=await (http??(model=="hy-mt"?HyClient:Client)).SendAsync(request,cancellationToken);
        response.EnsureSuccessStatusCode();using var json=JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
        var translated=json.RootElement.GetProperty("translations")[0].GetProperty("text").GetString();
        if(string.IsNullOrWhiteSpace(translated))throw new TranslatorException(TranslatorStatus.NeedsDownload);
        return name is null?translated:name+": "+translated;
    }
}
