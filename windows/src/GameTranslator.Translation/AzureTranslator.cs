// SPDX-License-Identifier: AGPL-3.0-only
using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.RegularExpressions;
using GameTranslator.Core;

namespace GameTranslator.Translation;

/// <summary>Official Microsoft text translation; one bounded request, no silent fallback.</summary>
public sealed class AzureTranslator : ITextTranslator
{
    private static readonly HttpClient Shared = new(new HttpClientHandler{AllowAutoRedirect=false}) { Timeout = TimeSpan.FromSeconds(4) };
    private readonly HttpClient http;
    private readonly string key, region;
    private readonly TranslationCache cache = new();
    public AzureTranslator(string key, string? region = null, HttpClient? http = null)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length > 1024 || key.Any(char.IsControl)) throw new ArgumentException("Invalid credential");
        this.key = key.Trim(); this.region = (region ?? "").Trim().ToLowerInvariant();
        if (this.region.Length > 64 || !Regex.IsMatch(this.region, "^[a-z0-9-]*$")) throw new ArgumentException("Invalid Azure region");
        this.http = http ?? Shared;
    }
    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(text)) return "";
        if (text.Length > 4000) throw new TranslatorException(TranslatorStatus.ModelRejected);
        if (cache.TryGet(text, out var known)) return known;
        var pieces = SubtitleSegmenter.Split(text);
        var missing = pieces.Where(p => p.Translate).Select(p => p.Text).ToArray();
        if (missing.Length == 0) return text;
        using var request = new HttpRequestMessage(HttpMethod.Post,
            "https://api.cognitive.microsofttranslator.com/translate?api-version=3.0&from=en&to=ar&textType=plain");
        request.Headers.Add("Ocp-Apim-Subscription-Key", key);
        if (region.Length > 0) request.Headers.Add("Ocp-Apim-Subscription-Region", region);
        request.Content = JsonContent.Create(missing.Select(s => new { Text = s }));
        try
        {
            using var response = await http.SendAsync(request, cancellationToken).ConfigureAwait(false);
            if (!response.IsSuccessStatusCode) throw new TranslatorException(response.StatusCode switch
            {
                HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => TranslatorStatus.KeyRejected,
                HttpStatusCode.TooManyRequests => TranslatorStatus.QuotaExceeded,
                HttpStatusCode.BadRequest => TranslatorStatus.ModelRejected,
                _ => TranslatorStatus.ServiceUnreachable
            });
            var json = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (json.Length > 128_000) throw new JsonException();
            using var doc = JsonDocument.Parse(json);
            var translated = doc.RootElement.EnumerateArray().Select(x => x.GetProperty("translations")[0].GetProperty("text").GetString()).ToArray();
            if (translated.Length != missing.Length || translated.Any(string.IsNullOrWhiteSpace)) throw new JsonException();
            int index = 0;
            var result = string.Concat(pieces.Select(p => p.Translate ? translated[index++] : p.Text));
            cache.Store(text, result); return result;
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested) { throw new TranslatorException(TranslatorStatus.ServiceUnreachable); }
        catch (HttpRequestException) { throw new TranslatorException(TranslatorStatus.ServiceUnreachable); }
        catch (Exception e) when (e is JsonException or InvalidOperationException or KeyNotFoundException or IndexOutOfRangeException) { throw new TranslatorException(TranslatorStatus.EngineFailed); }
    }
}
