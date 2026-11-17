# Windows 0.4.14 — interface and translation review

Prepared for testing on 17 November 2026. No new public package release is authorized by this work.

## Changes

- Keep four task tabs and the session start/test buttons. Wrap console/window/manual-region rows, wrap long button and checkbox labels, reduce tab header padding, and bound expanded live text to a scrolling area. Detailed console instructions are collapsed by default.
- Show only controls relevant to the selected translator: local model launch, cloud credentials, DeepL quality mode, or Azure resource region. Local testing no longer asks for an API key. Disable overlapping key tests and starting a session during a key test.
- Preserve saved font size, opacity and bottom position instead of rewriting defaults on every startup. Add small/medium font shortcuts. Persist the selected game so translation context does not revert to RDR2 on restart.
- Add Microsoft Azure Translator v3 as an optional English→Arabic comparison engine. It uses the official global HTTPS endpoint, a user-supplied encrypted resource key, and an optional resource region. Speaker labels remain outside translated sentence batches. Requests have a four-second timeout, no redirect following, no automatic retry or silent engine fallback. New session instances have separate in-memory caches. HTTP credential/quota/service failures are distinguished without exposing response bodies or keys.
- Improve AI instructions for idiomatic dialogue, negation, pronouns and context rather than literal word substitution. Add shared game context for AI and DeepL: RDR2 is a historical 1899 Western; other games do not inherit that setting. These are prompt/context changes, not model training, a replacement for a reviewed Arabic script, or proof of improved semantic quality.

## Upstream review

- [Sefflex v1.7.0](https://gitlab.com/sefflex/GameTranslator/-/releases/v1.7.0), released 16 November: release notes still describe DeepL and Gemini, multilingual/RTL rendering, QR browser second screen and direct Xbox beta. Previously reviewed integration gaps are not assumed solved by release claims. No zero-latency or untested native Xbox claim is adopted.
- [Nino current model catalog](https://github.com/yunusdemirbag/nino-game-translator/blob/ce16a7e1be4beb6cfcdcf2f93a918ae547172a0a/core/Sources/CouchSubsCore/Resources/ai-models.json): Gemini 3.5 Lite/3.6, OpenAI Luna/Terra and Claude Haiku/Sonnet are already available in our catalog. Its [Arabic prompt](https://github.com/yunusdemirbag/nino-game-translator/blob/ce16a7e1be4beb6cfcdcf2f93a918ae547172a0a/windows/src/CouchSubs.Translation/AiTranslator.cs) emphasizes natural readable Arabic and stable names, rather than a new Arabic model. README describes some upcoming setup work as Mac-only; it is not evidence that Windows already has every screenshot feature.
- [Hearth model sources](https://github.com/Sal7one/Hearth/blob/main/docs/model-sources.md) list Hy-MT2 1.8B and TranslateGemma 4B in addition to QuickMT/Jisr. Quantization/download size alone does not prove better speed or Arabic dialogue quality on the user's Ryzen 5. No large model is downloaded or enabled silently.
- Hearth [cloud translation codec](https://github.com/Sal7one/Hearth/blob/bc6823b8ec8254eefa222d28e60d70ccdd7d67ce/app/src/main/java/com/sal7one/transiber/translation/CloudTranslationProtocol.kt) confirms official Azure key/region request routing. Our C# adapter is independently implemented. [Azure official API contract](https://learn.microsoft.com/en-us/rest/api/translator/translator/translate?view=rest-translator-v3.0).
- Hearth desktop 1.0.8 adds Windows and a local approved-client service. A service bridge would require a separate installed/configured Hearth instance; it is not presented as a built-in translator in this update.

## Validation and comparison limits

Locally: WPF cross-build succeeds; 34 targeted translation regression tests passed, including eight new Azure/context cases. Windows CI also builds the executable, runs the broader regression filters, verifies dependency audit, renders eight setups (four tabs at normal and narrow sizes), checks saved style preservation, provider-specific fields and horizontal clipping, and tests update integrity/preservation. See the accompanying CI result for completion.

No live Azure/DeepL/Gemini account test or Ryzen/vivo/TCL hardware benchmark is available here. For quality, repeat the same scene and inspect English OCR first. Prior user log evidence favored DeepL for several meanings and QuickMT for latency; Azure is an additional candidate, not a declared winner. Existing comparison logging includes the actual engine/mode and timings. Translation timings exclude Remote Play and TV delay.

Install the small patch over an existing installation using Reading-Fix.cmd after extracting the whole ZIP. It keeps existing model/runtime files and encrypted settings and retains the previous installation. It does not add native console pairing or an interactive setup wizard.
