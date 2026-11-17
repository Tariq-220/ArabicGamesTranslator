# ArabicGames 0.4.9

- Fix Arabic prepositions in RDR2 character-name correction (Dutch).
- Keep a recent complete OCR reading when a fade-out loses words or joins them. Real meaning changes remain distinct.
- Add a small RDR2-only reviewed phrase dictionary; its output is marked separately from engine requests in the UI and comparison log. This is not a general model-quality upgrade.
- Record app edition and translation method in new comparison entries.
- Preserve user settings, API keys and the selected engine.

Validation: 30 translation/update/profile tests and 18 focused reading/pipeline tests passed locally; GitHub Actions performs the Windows build before publishing. No physical console/TV test in the build environment.
