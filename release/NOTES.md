# ArabicGames 0.4.10

- Add an explicit PS5 Remote Play path: launch the installed official player, select its window, and show setup guidance. Sign-in remains in Sony’s app; no native PS5 account implementation is claimed.
- Add an opt-in 40ms frame-sampling gap (balanced default remains 80ms). It may increase laptop load and does not reduce Remote Play or model inference latency.
- Record console selection and requested capture gap in comparison logs.
- Fix Arabic prepositions in RDR2 character-name correction (Dutch).
- Keep a recent complete OCR reading when a fade-out loses words or joins them. Real meaning changes remain distinct.
- Add a small RDR2-only reviewed phrase dictionary; its output is marked separately from engine requests in the UI and comparison log. This is not a general model-quality upgrade.
- Record app edition and translation method in new comparison entries.
- Preserve user settings, API keys and the selected engine.

Validation: 30 translation/update/profile tests and 23 focused reading/pipeline tests passed locally; GitHub Actions performs the Windows build before publishing. No physical console/TV test in the build environment.
