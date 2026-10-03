# Status and plan (handover)

Branch: `claude/charming-dirac-a5bqvd`. The goal is a production-ready, local-first, cross-platform alternative to
Acrobat Reader. See `docs/ACROBAT-PARITY.md`.

## Done and pushed

- Real-world PDF corpus tests: 683 of 683 pass.
- Tagged-PDF reading order, with layout inference as the fallback.
- Tagged structure surfaced to assistive technology.
- Listening corpus and `TextNormalizer`.
- Measuring tools.
- Folder-wide search.
- Avalonia upstream notes in `docs/upstream/avalonia/`.
- Voice release:
  - `.github/workflows/voice-models.yml` built and published GitHub release `voices-1`. It contains MeloTTS-English
    ONNX, BERT (10 layers, int8 per channel), the lexicon, the g2p_en weights, and a mirror of the Kokoro and misaki
    files.
  - The exported ONNX matches PyTorch to within 3e-5. BERT cosine similarity is 0.985. Real-time factor is 0.34 on 4
    threads.
  - Tools are in `tools/voice-models/`. Python is CI only and never ships.

## In progress: MeloTTS as the default on-device voice

The user's requirements:

- On device, with an Australian voice.
- Keep Kokoro, and let the user choose the engine.
- MeloTTS is the default.
- No Python.
- Models hosted on GitHub, not Hugging Face.
- BERT on.
- English only.

Written in this commit (`src/PdfViewerLite.Speech` builds; nothing is tested yet):

- `VoiceRelease.cs`: release URLs and pinned SHA-256 values copied from `voices.json`.
- `SpeechModelFile` now carries `Sha256`, and `SpeechModelDownloader` checks it.
- `KokoroModel` now downloads from the release.
- `Melo/`:
  - `WordPieceTokenizer`: BERT uncased.
  - `MeloSymbols`: symbols and the ARPAbet-to-id/tone mapping.
  - `MeloLexicon`
  - `SpellingToSound`: g2p_en GRU.
  - `MeloFrontEnd`: grouping, word2ph, blanks, tone start 7, language 2.
  - `MeloInput`
  - `MeloModel`: voices EN-AU (first), EN-BR, EN-US, EN_INDIA.
  - `MeloEngine`: BERT, then feature alignment, then the synthesizer at 44.1 kHz.
- `tests/PdfViewerLite.Core.Tests/Speech/Melo/melo-reference.json`: Python front-end reference output from
  `tools/voice-models/reference_melo.py`.

## Next steps, in order

1. Tests:
   - `MeloModelFixture`, a copy of `KokoroModelFixture` using `PDFVIEWERLITE_MELO_DIR`, default
     `~/.cache/pdfviewerlite/melo` (files already downloaded there in the dev box), and `PDFVIEWERLITE_MELO_DOWNLOAD`.
   - `MeloFrontEndTests`: compare `MeloFrontEnd.Load(dir).Prepare(case.text)` against the reference ids, tones,
     languages, wordToPhones and tokenIds, and check `SpellingToSound` predictions. Copy the JSON to output in the
     csproj and add source-generated JSON.
   - `MeloRealModelTests`, modelled on `KokoroRealModelTests` but without the repeatability check, because Melo adds
     noise: speech-like output, real-time factor below 1, every voice, long text, nothing to say.
   - Run the listening corpus on Melo.
2. App:
   - Add a `SpeechEngineChoice` value for Melo and make it the default. Use explicit enum values and keep the existing
     numbers stable.
   - `SpeechSetup.CreateEngineFor` and its voice files should be per engine.
   - Preferences gets three engine options: MeloTTS (default), Kokoro, Azure.
   - Voice list defaults to EN-AU.
   - Update the CI cache for `~/.cache/pdfviewerlite/melo`.
3. Benchmarks:
   - Melo front end Prepare and engine Synthesize.
   - Explain allocations in `benchmarks/allocations-explained.json`.
   - `scripts/audit-allocations.sh`.
4. Docs: `docs/LISTENING.md` (MeloTTS default, Kokoro optional), README voice row and licences (MeloTTS MIT,
   bert-base-uncased Apache-2.0, g2p_en Apache-2.0), and `ACROBAT-PARITY.md`.
5. Full `dotnet build PdfViewerLite.slnx`, `dotnet test`, and an AOT publish check.

## Remaining goal items after voices

- #32 Shape, arrow and stamp annotations.
- #33 Comment replies and review status.
- #34 Booklet and poster printing.
- #35 Timestamp and LTV signature validation.
- #36 Safe form JavaScript (XFA is low priority).

## Notes

- To publish new voice files, bump `RELEASE_TAG` in the workflow and `ReleaseTag` in `VoiceRelease.cs`, then
  re-pin the hashes. A published release is never changed.
- The dev box has about 3.5 GB of disk free.
