// SPDX-License-Identifier: AGPL-3.0-only

using GameTranslator.Core.Dubbing;

namespace GameTranslator.Core;

/// <summary>Bir satırı oyun dilinden hedef dile çevirir (cihaz üstünde).</summary>
public interface ITextTranslator
{
    Task<string> TranslateAsync(string text, CancellationToken cancellationToken = default);
    int PreferredBatchSize=>24;
    /// <summary>Independent spatial pieces; providers can batch transport without merging text.</summary>
    async Task<IReadOnlyList<string>> TranslateManyAsync(IReadOnlyList<string> texts,CancellationToken cancellationToken=default){var result=new List<string>();foreach(var text in texts)result.Add(await TranslateAsync(text,cancellationToken).ConfigureAwait(false));return result;}


    /// <summary>Independent conversational context for menus/phone; stateless engines may share themselves.</summary>
    ITextTranslator ForIndependentArea() => this;

    /// <summary>Allows one independent area request alongside dialogue. False for shared local inference or fallbacks.</summary>
    bool SupportsConcurrentAreas => false;

    /// <summary>Son çevrilen altyazının seslendirilecek cümleleri (konuşmacısıyla); dublaj için.</summary>
    IReadOnlyList<SpokenLine> SpokenLines(string text) => [];

    /// <summary>
    /// Çevirmen <see cref="Prefetch"/> destekliyor mu. Destekleyen (ör. çevrimiçi) çevirmende okuma hattı satırı ilk
    /// karede çevirmeye başlar; temiz okunmayan satırı TV'ye ancak sonraki kare doğrulayınca gönderir: altyazı
    /// belirirken okunan bozuk ilk kare ekrana hiç çıkmaz, bekleme de çeviri süresine gizlenir.
    /// </summary>
    bool Prefetches => false;

    /// <summary>Satırın çevirisini arka planda başlatır; aynı metin için sonraki <see cref="TranslateAsync"/> onu bekler.</summary>
    void Prefetch(string text) { }
}

/// <summary>Optional bounded diagnostic output; never used as displayable text.</summary>
public interface IRejectedTranslationDiagnostics
{
    string? RejectedOutput(string source);
}

public enum TranslatorStatus
{
    Ready,
    /// <summary>Çeviri modeli henüz indirilmedi.</summary>
    NeedsDownload,
    /// <summary>Bu dil çifti desteklenmiyor.</summary>
    Unsupported,
    /// <summary>
    /// Model kurulu ama cihaz üstü çeviri motoru bu bilgisayarda açılamadı (ör. ONNX Runtime'ın yerel DLL'i
    /// yüklenemedi). Kullanıcı çevrimiçi bir motor seçebilir; oyun paketleri çalışmayı sürdürür.
    /// </summary>
    EngineFailed,
    ServiceUnreachable,
    KeyRejected,
    QuotaExceeded,
    ModelRejected,
    OutputRejected,
    ServiceBusy,
}

/// <summary>Kullanıcının düzeltebileceği çeviri hatası (model eksik, dil desteklenmiyor, motor açılamadı).</summary>
public sealed class TranslatorException(TranslatorStatus status, Exception? innerException = null)
    : Exception($"Translator not available: {status}", innerException)
{
    public TranslatorStatus Status { get; } = status;
}

/// <summary>
/// Çevrimiçi çeviri anahtarı (DeepL ya da yapay zekâ hizmeti) denendiğinde ne çıktı. <c>ModelRejected</c>: hizmet model
/// adını kabul etmedi (yalnız yapay zekâ hizmetlerinde).
/// </summary>
public enum OnlineKeyState { Valid, Invalid, QuotaExceeded, Unreachable, ModelRejected }

/// <param name="Used">Bu ay kullanılan karakter (geçerli anahtarda).</param>
/// <param name="Limit">Aylık sınır.</param>
public sealed record OnlineKeyCheck(OnlineKeyState State, long Used = 0, long Limit = 0);


public static class CloudFailureStatus
{
    public static TranslatorStatus From(OnlineKeyState? state) => state switch {
        OnlineKeyState.Invalid => TranslatorStatus.KeyRejected,
        OnlineKeyState.QuotaExceeded => TranslatorStatus.QuotaExceeded,
        OnlineKeyState.ModelRejected => TranslatorStatus.ModelRejected,
        _ => TranslatorStatus.ServiceUnreachable };
}
