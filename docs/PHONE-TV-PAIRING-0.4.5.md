# Phone 0.4.5 / Google TV 0.3.3 — test build

## Connection

1. Install both matching APKs; keep the existing applications and their data.
2. On the phone home screen, press تشغيل الربط / إرسال جملة اختبار.
3. On Google TV press اكتشاف الجوال أو الكمبيوتر. Both devices must be on the same private IPv4 LAN without client isolation.
4. Select the phone, open the phone home screen, compare the six-character challenge with the TV, and approve. No IP or pairing-code typing is needed.
5. The TV saves the phone address and pairing code for subsequent connections. Manual connection remains an alternative under settings.

Discovery is bounded to up to two private /24 subnets with 16 workers, 450ms timeouts, and a 25s search budget. It is not guaranteed to work across guest networks, VLANs, or VPN routes. A saved address may need rediscovery after DHCP changes. This release does not add a native TV QR scanner; the existing browser second-screen QR is separate.

## Security boundaries

`/hello` advertises phone capability only, never the pairing code or subtitles. `/pair` requires a 128-bit random request ticket, private IPv4 peer, no browser Origin, an explicit in-app approval, the same requester's IP and ticket, and a 60-second pairing window. One pending request is retained at a time. Rejected requests do not obtain the session code. Existing subtitle endpoints still require the pairing code. The phone home screen must be open to approve a new request.

LAN HTTP remains unencrypted. Use a trusted LAN; this change does not claim TLS, resistance to a malicious LAN observer, native Xbox/PS5 pairing, or guaranteed Android screen-off capture.

## Translation quality

A user-reviewed correction in the private phone history is reused before the selected translation engine or its cache, for the exact game and English text (whitespace normalized only). Machine output is never automatically promoted to a reviewed correction, and human wording bypasses the automatic game-specific Arabic rewrite. Existing history retains only 200 entries; deleting history removes those corrections. This is local correction reuse, not training or a permanent corpus.

The history flags outputs without Arabic or with a run of untranslated English words. These are heuristics; they cannot verify meaning. No extra paid AI request, upload, silent engine fallback, or semantic quality claim is introduced.

## Validation

15 phone tests passed: pairing approval/privacy, game-scoped reviewed lookup, quality hints, engine settings, portrait/landscape layout and OLED cover behavior. Four TV tests passed: private subnet restriction, caption settings and compact layout. Portrait and TV render previews were visually inspected. Release APKs were built locally; real vivo/TCL/console discovery and playback remain to be tested by the user.

The Windows executable is unchanged in this update. The interactive setup wizard is deferred as requested. These APKs are prepared for testing, not published as a new public release.
