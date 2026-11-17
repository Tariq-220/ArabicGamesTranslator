# Source review and activation paths

Sources inspected: Sefflex/GameTranslator GitLab release/tag v1.7.0 and yunusdemirbag/nino-game-translator main on 2026-11-17. Descriptions are claims; physical console performance was not verified.

Sefflex release notes advertise direct Xbox device-code home streaming and a scripts/xbox-bridge directory. That directory and a Windows Xbox session implementation were absent from the tag's source tree; the README's marketing claims cannot establish a working import. The tag's webOS source itself reports 1.0.5 despite the release notes saying packages 1.0.7. Our fork therefore packages and identifies its own adapted webOS source as 1.0.8, retaining AGPL notices.

Nino documents native Android Xbox home streaming using Microsoft device code, Xbox Live/XSTS/xHome and native org.webrtc. Its six Xbox classes total roughly 919 lines and depend on its own frame-source/reconnect/UI/storage stack. Windows reads the Xbox window. The Android implementation is a credible follow-up integration candidate, not a drop-in bridge for this Java/WebView app. No Nino source was imported in this release.

Adopted: provider key shortcuts, provider/model selection, device-specific activation guide, existing SSDP TV pairing exposed in our UI, bounded private-LAN desktop discovery on Google TV, webOS companion, and QR second-screen workflow. API quota, model access, and translation quality remain account/model dependent; no secret keys or user comparison logs are published.

Windows source plays on HDMI while reading a visible Remote Play window. Monitor mode uses the Windows overlay if the game is shown on that PC; QR displays captions on a separate browser device, not above arbitrary HDMI input. iOS-only ingestion and native Mac packaging are deferred by user instruction.
