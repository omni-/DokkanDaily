# Screenshot stage validation

Uploads independently read the visible event title, stage title and difficulty, then
compare them with the server-assigned challenge. Accepted uploads can score;
clear event/stage mismatches and known lower difficulties reject before storage.
Unknown results are accepted for display and scoring.
Missing data never establishes a mismatch. Validation outcomes
remain in metadata for diagnostics. Missing aliases remain unknown.
All 156 current configured stages have sourced English alias pairs. This is an
accidental-upload check, not proof of completion or team compliance.

The production implementation preserves the final September 2026 OCR candidate:
geometry, routing, matching thresholds and model are frozen. The alias catalog is
maintained by `python scripts/sync-stage-links.py` alongside stage links. It fetches
English event names and numbered stage titles from Dokkan Info, preserves reviewed
localizations and historical contrast entries, and refuses incomplete coverage.
The [research release](https://github.com/omni-/DokkanDaily/releases/tag/ocr-test)
contains the full September 2026 research/evaluation provenance, including the
completed held-out evaluation. Corpora, training tools and exhaustive evaluation
matrices are intentionally absent from this production patch.

`jpn-stage-v2.traineddata` is a required runtime model (SHA-256
`0bf65e5fe79140725174ca41d4f1a193c8f2232224e9df31dba657cf928fdf5e`).
The project embeds the alias and Unicode case-fold resources and publishes models
through its existing wwwroot content rule. The existing Dockerfile supplies native
OpenCV, Tesseract and Leptonica; no new Docker dependency is required.

## Verification

Run `dotnet test tests/DokkanDailyTests/DokkanDailyTests.csproj -c Release`.
`StageValidationTests` covers conservative text rules, catalog coverage, upload
rejection and two frozen native observations using screenshots already in the
base repository. The compact fixture is extracted unchanged from the archived
Windows runtime-v3 and Linux observations for `seed-heroine-global-chi-chi` and
`seed-heroine-jp-android18`. Windows and Linux expectations are separate because
native OCR output differs between platforms. No research images are required.

For production packaging, build the Dockerfile and `tools/OcrSmoke/OcrSmoke.csproj`
in Release. Stage `runtime-regression.json` plus its two referenced images into a
fixture directory. Mount that directory at `/fixtures` and only `OcrSmoke.dll` at
`/app/OcrSmoke.dll`, both read-only. Run the image with `--network none`,
`--entrypoint dotnet`, and arguments:

```
exec --runtimeconfig /app/DokkanDaily.runtimeconfig.json --depsfile /app/DokkanDaily.deps.json /app/OcrSmoke.dll /fixtures
```

This exercises the final image's application, native libraries, all three OCR
models, embedded catalog and conservative matcher. It does not run research sweeps
or the held-out evaluation. The bundled Japanese model may emit an existing warning
about absent vertical-language data; horizontal recognition remains the frozen path.
