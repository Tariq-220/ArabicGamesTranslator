# PS5 and TV controls — phone 0.4.6 / TV 0.3.4

Prepared for testing, 18 November 2026. No new public application release is published.

## PS5: two accounts for the TV/gamepad setup

For playing on the television with a physical DualSense and using a separate Remote Play session for subtitle reading:

1. On PS5, add/sign in to a free secondary PSN user. Remain on that user while enabling Remote Play and pairing.
2. Enable Settings → System → Remote Play → Enable Remote Play.
3. In Sony PS Remote Play on Android, sign in to the same secondary account. If it is already signed in as the primary, sign out there first. Connect to PS5; complete any device-link code inside Sony's app. Verify actual game video before continuing.
4. On the physical controller connected directly to PS5, select the primary game-playing user and open the game. Keep the phone stream signed in as secondary. Enable English subtitles.
5. In our phone app choose PS5, follow the setup pages, pair the television, then press بدء ترجمة PS5 and grant Android's screen-read permission. If Android offers app-only sharing, choose Sony PS Remote Play. After permission, our app opens Sony automatically.
6. If switching users disconnects Sony's stream, restore the secondary stream first and verify the controller's selected user. Behavior can vary with Sony's client/firmware. A secondary account is not a universal requirement for controlling the game directly from the phone; the alternate phone-control setup says this explicitly.

An already configured stream can use «البث مربوط مسبقًا — ابدأ الترجمة» to go straight to translation without repeating login/setup.

The new app workflow separates secondary-on-console, secondary-on-phone, primary-on-controller and translation start into distinct pages. Reopening it from Home reuses the existing setup page. Denied consent, a missing translation model, or a missing Sony application cannot silently start reading another app.

This is an **external Sony app capture workflow**. Native PS5 discovery, Sony OAuth, device-code registration and native video decoding inside our application are not integrated. Connecting Sony Remote Play does not grant our application access to its frames. A secure/black Sony surface cannot be made readable by changing UI steps. If Android prevents capture, use the Windows Remote Play window path instead. No real PS5/Sony/vivo session was available for verification.

Our own app's foreground settings pages are excluded from external projection OCR, so engine/connection UI does not become dialogue. This does not protect other applications during full-display sharing; choose app-only sharing where offered. It does not turn off the physical phone display while preserving a protected stream.

## Google TV / TCL control repair

- Do not auto-tune saved HDMI on application startup. Restore the selection and open the controllable panel first.
- Block TvView descendants from taking keyboard focus. A remote key handler intercepts panel shortcuts before the HDMI view consumes them.
- Back/Menu toggles the panel; OK/Enter or an arrow restores it while hidden. Consume matching key-up so reopening does not accidentally click a different action. Info opens the panel; the red key tests Arabic.
- Keep a small settings handle when the main panel is hidden. A failed/missing HDMI selection leaves the controls available.
- A phone Home button sends a settings event to already authenticated SSE receivers to reopen their panel. This is not a new unauthenticated HTTP command endpoint. It requires the receiver activity to be running and connected over SSE; it is not a remote app launcher or a polling fallback.
- If the TV firmware switches to a separate native HDMI application, use Home and reopen our app. App-level key interception cannot control an activity owned by the manufacturer or override HDMI-CEC globally.

## Source review

[SARD](https://playinarabic.com/sard): public site and its public setup guide describe secondary PSN first, same secondary on phone, then primary for playing; a short code for TV pairing; local OCR; an initial fast translation later replaced by a refined one; and a paid service/free trial. The site does not identify the translation model in the material inspected. Its less-than-one-second timing is a provider claim, not our measurement. No model credentials, subscription or service account was accessed.

The supplied [short link](https://t.co/oBiUofDAHz) resolves to https://d3athly.github.io/ and points to [BlackBox GameTranslator](https://github.com/D3ATHLY/GameTranslator). Its repository actually contains source even though README.md is empty. Reviewed tree SHA: 30d2639e3d91d97520eb2a27fd8d6c5c12f7723d.

- [TV key dispatch](https://github.com/D3ATHLY/GameTranslator/blob/main/tv/android/app/src/main/java/com/gametranslator/tv/MainActivity.java), blob c438b1de272a2b7f9229c73a01428b767f0101d0: dispatches remote keys before the embedded web UI. Our independently implemented native-panel repair uses that same useful interception point rather than importing its WebView UI.
- [Model catalog](https://github.com/D3ATHLY/GameTranslator/blob/main/windows/src/GameTranslator.Core/Resources/ai-models.json), blob d249b524b7b7b2fbcce57fea1282e8553793a044: Gemini 3.5 Lite/3.6, OpenAI Luna/Terra, Claude Haiku/Sonnet are already represented in our application. It does not demonstrate a new, more accurate Arabic engine.
- [Android native bridge](https://github.com/D3ATHLY/GameTranslator/blob/main/mobile/android/app/src/main/kotlin/com/gametranslator/gametranslator_mobile/MainActivity.kt), blob 91b9cccf530ed2b53b23efff61136494dbc71aaf: Flutter method channel calls an Engine registration/session implementation with C++/CMake dependencies. It is not a connection that can be inferred from Sony's running application or enabled by adding an IP field. It has not been wired into our Java application in this patch.
- [Nino setup guide](https://github.com/yunusdemirbag/nino-game-translator/blob/main/docs/KURULUM.md) likewise explains secondary PSN, pairing and separate TV/gamepad play. Its native PS5 path is distinct from our external Sony client workflow.

## Validation

Robolectric covers step navigation and rotation, appropriate account instructions, consent/model/app prerequisites, own-UI capture suppression state, remote down/up interception, returning from hidden panel, HDMI view focus blocking, missing-input behavior, paired relay control delivery, existing phone portrait/landscape and TV style/layout behavior. Release APKs use the same existing certificate as the previous test APKs. Hardware-specific HDMI-CEC, firmware routing, Sony capture permission and actual translation timing still require user testing.

Validation completed: 27 distinct tests passed across the targeted phone/TV checks (including six PlayStation-flow cases after the final shortcut). Release APK builds and certificate continuity checks passed. Portrait PS5 setup/start previews were visually inspected.
