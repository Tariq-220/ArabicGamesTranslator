// SPDX-License-Identifier: AGPL-3.0-only

using System.Collections.Concurrent;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json.Serialization;
using GameTranslator.Core;
using GameTranslator.Core.Dubbing;

namespace GameTranslator.Translation;

/// <summary>
/// İsteğe bağlı çevrimiçi çeviri: kullanıcının kendi DeepL anahtarıyla. Altyazı cümle cümle, konuşmacı adları
/// çevrilmeden gönderilir. Son gösterilen 3 satır bağlam olarak eklenir: DeepL'in <c>context</c> alanı (tek metin)
/// çeviriyi etkiler ama çevrilmez ve ücretlendirilmez ("Characters included in the context parameter will not be
/// counted toward billing", developers.deepl.com/docs/api-reference/translate, Kasım 2026). Önbellekteki çeviri
/// bağlamsız eski haliyle döner; anahtar yalnız satırın kendisidir.
/// DeepL'e ulaşılamazsa ya da kota biterse o satır cihaz üstü modelle çevrilir; oyun hiç altyazısız kalmaz.
/// </summary>
public sealed class DeepLTranslator : ITextTranslator, IOnlineTranslator
{
    /// <summary>Kalıcı önbellek dosyasındaki motor adı.</summary>
    public const string EngineId = "deepl";
    /// <summary>Bağlama giden önceki satır sayısı.</summary>
    internal const int ContextLines = 3;
    /// <summary>Hata sonrası bu süre DeepL denenmez; her satırda bağlantı zaman aşımını beklemeyelim.</summary>
    private static readonly TimeSpan RetryAfter = TimeSpan.FromMinutes(1);

    private static readonly Lazy<HttpClient> SharedClient = new(() => new HttpClient { Timeout = TimeSpan.FromSeconds(4) });

    private readonly string _key;
    private readonly bool rememberDialogue,validateArabic;
    private readonly Func<DateTimeOffset> now;
    private readonly string? _modelType,_game;
    public string? ActualModelType {get;private set;}
    private readonly ITextTranslator? _fallback;
    private readonly HttpClient _http;
    private readonly TranslationCache _cache;
    private readonly ConcurrentDictionary<string,byte> rephrased=new(StringComparer.Ordinal);
    private bool Cached(string source,HashSet<string> retry,out string value){if(!_cache.TryGet(source,out value))return false;if(!validateArabic||!ArabicRegister.NeedsRephrase(source,value)||rephrased.ContainsKey(source))return true;retry.Add(source);return false;}
    private void Rephrased(IEnumerable<string> sources){if(rephrased.Count>=1024)rephrased.Clear();foreach(var source in sources)rephrased[source]=0;}
    private readonly ConcurrentDictionary<string, IReadOnlyList<SpokenLine>> _speech = new(StringComparer.Ordinal);
    private readonly ConcurrentDictionary<string, byte> _names = new(StringComparer.Ordinal);
    private readonly List<string> _recent = [];
    private DateTimeOffset _pausedUntil = DateTimeOffset.MinValue;

    /// <param name="cache">Kalıcı önbellek; verilmezse yalnız bellekte tutulur.</param>
    public DeepLTranslator(string key, ITextTranslator? fallback, HttpClient? http = null, TranslationCache? cache = null,string? modelType=null,string? game=null,bool rememberDialogue=true,bool validateArabic=false,Func<DateTimeOffset>? clock=null)
    {
        if(modelType is not (null or "latency_optimized" or "quality_optimized" or "prefer_quality_optimized"))throw new ArgumentException("Unsupported DeepL model type",nameof(modelType));
        _modelType=modelType;_game=game;
        this.rememberDialogue=rememberDialogue;this.validateArabic=validateArabic;now=clock??(()=>DateTimeOffset.UtcNow);
        _key = key.Trim();
        _fallback = fallback;
        _http = http ?? SharedClient.Value;
        _cache = cache ?? new TranslationCache(targetLanguage:validateArabic?"ar":null);
    }

    /// <summary>Son DeepL hatası; çeviri düzgün gidiyorsa null.</summary>
    public OnlineKeyState? LastError { get; private set; }

    public event Action<OnlineKeyState?>? FailureChanged;

    private void Report(OnlineKeyState? state)
    {
        if (LastError == state) return;
        LastError = state;
        FailureChanged?.Invoke(state);
    }

    /// <summary>Ücretsiz anahtarlar ":fx" ile biter ve ayrı adrese gider.</summary>
    internal static Uri BaseUri(string key) =>
        new(key.Trim().EndsWith(":fx", StringComparison.Ordinal) ? "https://api-free.deepl.com/v2/" : "https://api.deepl.com/v2/");

    public async Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(text);
        cancellationToken.ThrowIfCancellationRequested();
        // Bağlam, bu satırdan önce gösterilenlerdir; satırın kendisi bağlama girmez.
        var context = Context()+"\n"+ArabicRegister.Instruction;
        var hint=CommunityCorrections.Hints(_game??"",text);if(hint.Length>0)context=string.Join("\n",new[]{context,hint}.Where(s=>!string.IsNullOrWhiteSpace(s)));
        if (_game is {Length:>0})context=string.Join("\n",new[]{context,(rememberDialogue?"Current complete dialogue: ":"Current screen text: ")+text}.Where(s=>!string.IsNullOrEmpty(s)));
        var retry=new HashSet<string>(StringComparer.Ordinal);
        if (Cached(text,retry,out var cached)){RememberIfValid(text,cached);return cached;}


        var pieces = SubtitleSegmenter.Split(text);
        foreach (var name in pieces.Select(SubtitleSegmenter.LabelName).OfType<string>())
            foreach (var word in name.Split(' ', StringSplitOptions.RemoveEmptyEntries).Where(w => w.Length >= 2))
                _names[word.ToLowerInvariant()] = 0;
        var names = _names.Keys.ToHashSet(StringComparer.Ordinal);
        // Önbellekte duran cümleler yeniden gönderilmez; kota yalnız yeni cümlelere gider.
        var translations = pieces.Select(p => p.Translate && Cached(p.Text,retry,out var known) ? known : null).ToList();
        var missing = Enumerable.Range(0, pieces.Count).Where(i => pieces[i].Translate && translations[i] is null).ToList();
        var sources = missing.Select(i => SubtitleSegmenter.NormalizeCase(pieces[i].Text, names)).ToList();
        string? cachedFallback=null;
        if(validateArabic&&retry.Count>0){var old=new StringBuilder();bool complete=true;foreach(var piece in pieces){if(!piece.Translate)old.Append(piece.Text);else if(_cache.TryGet(piece.Text,out var value))old.Append(value);else{complete=false;break;}}if(complete){var value=ArabicGameNames.Localize(text,old.ToString());if(ArabicOutputQuality.Accepts(text,value))cachedFallback=value;}}
        if(now()<_pausedUntil)return cachedFallback??await FallbackAsync(text,cancellationToken).ConfigureAwait(false);


        string[] translated;
        try
        {
            translated = sources.Count == 0 ? [] : await RequestAsync(sources, context, cancellationToken).ConfigureAwait(false);
            Report(null);
        }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException or DeepLException
                                      && !cancellationToken.IsCancellationRequested)
        {
            Report(error is DeepLException deepL ? deepL.State : OnlineKeyState.Unreachable);
            var pause=error is DeepLException failed?failed.RetryDelay:TimeSpan.FromSeconds(2);
            _pausedUntil = now() + pause;
            return cachedFallback??await FallbackAsync(text, cancellationToken).ConfigureAwait(false);
        }
        for (var i = 0; i < missing.Count; i++)
        {
            translations[missing[i]] = translated[i];
            _cache.Store(pieces[missing[i]].Text, translated[i]);
        }

        var builder = new StringBuilder();
        for (var i = 0; i < pieces.Count; i++) builder.Append(translations[i] ?? pieces[i].Text);
        if (_speech.Count >= 512) _speech.Clear();
        _speech[text] = SpeechScript.Lines(pieces, translations);
        var result = builder.ToString();
        cancellationToken.ThrowIfCancellationRequested();
        _cache.Store(text, result);Rephrased(retry);
        RememberIfValid(text,result);
        return result;
    }

    public async Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> texts,CancellationToken cancellationToken=default)
    {
        if(texts.Count is <1 or >24)throw new ArgumentException("Invalid spatial batch size.");
        var result=new string[texts.Count];var missing=new List<int>();var retry=new HashSet<string>(StringComparer.Ordinal);
        for(int i=0;i<texts.Count;i++){if(Cached(texts[i],retry,out var hit))result[i]=hit;else missing.Add(i);}
        if(missing.Count==0)return result;
        if(now()<_pausedUntil){foreach(var i in missing)result[i]=retry.Contains(texts[i])&&_cache.TryGet(texts[i],out var old)&&ArabicOutputQuality.Accepts(texts[i],old)?old:await FallbackAsync(texts[i],cancellationToken).ConfigureAwait(false);return result;}
        try{
            var sources=missing.Select(i=>texts[i]).ToList();var context=Context()+"\n"+ArabicRegister.Instruction+"\nIndependent screen items (retain each item separately):\n"+string.Join("\n",sources);
            var translated=await RequestAsync(sources,context,cancellationToken).ConfigureAwait(false);Report(null);
            for(int n=0;n<missing.Count;n++){int i=missing[n];var value=validateArabic?ArabicGameNames.Localize(texts[i],translated[n]):translated[n];
                result[i]=value;if(!validateArabic||ArabicOutputQuality.Accepts(texts[i],value))_cache.Store(texts[i],value);}
            Rephrased(retry);return result;
        }catch(Exception error)when(error is HttpRequestException or TaskCanceledException or DeepLException&&!cancellationToken.IsCancellationRequested){
            Report(error is DeepLException deepL?deepL.State:OnlineKeyState.Unreachable);_pausedUntil=now()+(error is DeepLException failed?failed.RetryDelay:TimeSpan.FromSeconds(2));
            foreach(var i in missing)result[i]=retry.Contains(texts[i])&&_cache.TryGet(texts[i],out var old)&&ArabicOutputQuality.Accepts(texts[i],old)?old:await FallbackAsync(texts[i],cancellationToken).ConfigureAwait(false);return result;
        }
    }

    public bool SupportsConcurrentAreas => _fallback is null;

    public ITextTranslator ForIndependentArea() => new DeepLTranslator(_key,_fallback,_http,cache:new TranslationCache(targetLanguage:validateArabic?"ar":null),
        modelType:_modelType,game:_game,rememberDialogue:false,validateArabic:validateArabic,clock:now);

    private void RememberIfValid(string source,string result)
    {
        if(rememberDialogue&&(!validateArabic||ArabicOutputQuality.Accepts(source,result)))Shown(source);
    }

    /// <summary>Son gösterilen satırlar, eskiden yeniye; DeepL'e bağlam olarak gider.</summary>
    private string? Context()
    {
        lock (_recent){
            var prefix=string.IsNullOrWhiteSpace(_game)?null:rememberDialogue?DialogueContext.ForGame(_game):"On-screen game menus and phone messages. Game: "+_game;
            return prefix==null?(_recent.Count==0?null:string.Join("\n",_recent)):prefix+(_recent.Count==0?"":"\nPrevious dialogue:\n"+string.Join("\n",_recent));
        }
    }

    private void Shown(string text)
    {
        lock (_recent)
        {
            // Aynı satır yeniden okunduysa bağlam tekrarla dolmasın.
            if (_recent.Count > 0 && _recent[^1] == text) return;
            _recent.Add(text);
            if (_recent.Count > ContextLines) _recent.RemoveAt(0);
        }
    }

    public IReadOnlyList<SpokenLine> SpokenLines(string text) =>
        _speech.TryGetValue(text, out var lines) ? lines
        : SpeechScript.FromCache(text, _cache) is { Count: > 0 } cached ? cached
        : _fallback?.SpokenLines(text) ?? [];

    private async Task<string> FallbackAsync(string text, CancellationToken cancellationToken) =>
        _fallback is null ? throw new TranslatorException(CloudFailureStatus.From(LastError))
            : await _fallback.TranslateAsync(text, cancellationToken).ConfigureAwait(false);

    private async Task<string[]> RequestAsync(List<string> sentences, string? context, CancellationToken cancellationToken)
    {
        // Oyun konuşması: "siz" değil "sen" (DeepL Türkçede desteklemiyorsa yok sayar).
        var body = new TranslateRequest(sentences, "EN", "AR", context, "default", true,_modelType);
        using var request = new HttpRequestMessage(HttpMethod.Post, new Uri(BaseUri(_key), "translate"))
        {
            Content = JsonContent.Create(body, DeepLJson.Default.TranslateRequest),
        };
        request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", _key);
        using var response = await _http.SendAsync(request, cancellationToken).ConfigureAwait(false);
        ThrowIfFailed(response);
        var reply = await response.Content.ReadFromJsonAsync(DeepLJson.Default.TranslateResponse, cancellationToken).ConfigureAwait(false);
        if (reply?.Translations is not { } list || list.Count != sentences.Count) throw new DeepLException(OnlineKeyState.Unreachable);
        ActualModelType=list.Select(t=>t.ModelTypeUsed).FirstOrDefault(x=>!string.IsNullOrWhiteSpace(x));
        return [.. list.Select(t => t.Text)];
    }

    /// <summary>Ayarlardaki "Dene" düğmesi: anahtar geçerli mi, bu ay ne kadar kullanıldı.</summary>
    public static async Task<OnlineKeyCheck> CheckAsync(string key, HttpClient? http = null, CancellationToken cancellationToken = default)
    {
        key = key.Trim();
        if (key.Length == 0) return new(OnlineKeyState.Invalid);
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, new Uri(BaseUri(key), "usage"));
            request.Headers.Authorization = new AuthenticationHeaderValue("DeepL-Auth-Key", key);
            using var response = await (http ?? SharedClient.Value).SendAsync(request, cancellationToken).ConfigureAwait(false);
            ThrowIfFailed(response);
            var usage = await response.Content.ReadFromJsonAsync(DeepLJson.Default.UsageResponse, cancellationToken).ConfigureAwait(false);
            if (usage is null) return new(OnlineKeyState.Unreachable);
            return new(usage.CharacterLimit > 0 && usage.CharacterCount >= usage.CharacterLimit ? OnlineKeyState.QuotaExceeded : OnlineKeyState.Valid,
                usage.CharacterCount, usage.CharacterLimit);
        }
        catch (DeepLException error) { return new(error.State); }
        catch (Exception error) when (error is HttpRequestException or TaskCanceledException && !cancellationToken.IsCancellationRequested)
        {
            return new(OnlineKeyState.Unreachable);
        }
    }

    private static void ThrowIfFailed(HttpResponseMessage response)
    {
        if (response.IsSuccessStatusCode) return;
        var state=response.StatusCode switch
        {
            HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden => OnlineKeyState.Invalid,
            (HttpStatusCode)456 or HttpStatusCode.TooManyRequests => OnlineKeyState.QuotaExceeded,
            HttpStatusCode.BadRequest=>OnlineKeyState.ModelRejected,
            _ => OnlineKeyState.Unreachable,
        };
        var hint=response.Headers.RetryAfter;
        var delay=hint?.Delta ?? (hint?.Date is {} date?date-DateTimeOffset.UtcNow:(TimeSpan?)null);
        if(delay is not null)delay=TimeSpan.FromSeconds(Math.Clamp(delay.Value.TotalSeconds,1,120));
        throw new DeepLException(state,delay ?? (response.StatusCode==HttpStatusCode.TooManyRequests?TimeSpan.FromSeconds(3):state==OnlineKeyState.Unreachable?TimeSpan.FromSeconds(2):RetryAfter));
    }
}

internal sealed class DeepLException(OnlineKeyState state,TimeSpan? retryDelay=null) : Exception($"DeepL: {state}")
{
    public OnlineKeyState State { get; } = state;
    public TimeSpan RetryDelay {get;}=retryDelay??TimeSpan.FromSeconds(2);
}

internal sealed record TranslateRequest(
    [property: JsonPropertyName("text")] IReadOnlyList<string> Text,
    [property: JsonPropertyName("source_lang")] string SourceLang,
    [property: JsonPropertyName("target_lang")] string TargetLang,
    [property: JsonPropertyName("context"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] string? Context,
    [property: JsonPropertyName("formality")] string Formality,
    [property: JsonPropertyName("preserve_formatting")] bool PreserveFormatting,
    [property: JsonPropertyName("model_type"),JsonIgnore(Condition=JsonIgnoreCondition.WhenWritingNull)] string? ModelType=null);

internal sealed record TranslateResponse([property: JsonPropertyName("translations")] IReadOnlyList<TranslatedText>? Translations);

internal sealed record TranslatedText([property: JsonPropertyName("text")] string Text,[property:JsonPropertyName("model_type_used")] string? ModelTypeUsed=null);

internal sealed record UsageResponse(
    [property: JsonPropertyName("character_count")] long CharacterCount,
    [property: JsonPropertyName("character_limit")] long CharacterLimit);

[JsonSerializable(typeof(TranslateRequest))]
[JsonSerializable(typeof(TranslateResponse))]
[JsonSerializable(typeof(UsageResponse))]
internal sealed partial class DeepLJson : JsonSerializerContext;

