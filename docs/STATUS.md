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

## Done: MeloTTS as the default on-device voice

- The C# front end matches the Python original: `MeloFrontEndTests`, 32 checks.
- Real-model tests and the listening corpus pass on MeloTTS.
- Preferences offers MeloTTS (the default, Australian voice first), Kokoro and Azure.
- Benchmarks are in `MeloBenchmarks`, and their allocations are explained.
- Docs updated: `LISTENING.md`, the README and `ACROBAT-PARITY.md`.

## Done after the voices

- #32: shape, arrow and stamp annotations.
- #33: comment replies and review status. Replies are saved with standard /IRT references.
- #34: booklet and poster printing.
- #35: trusted timestamps, document timestamps, and the document security store (LTV). There is also a timestamp
  server setting for signing.
- #36: Acrobat's built-in form scripts, run as C#. JavaScript is never run.

## What is left

- Verified on Linux: the full suite with the real-world corpus downloaded passes (1,409 passed, 0 failed, 6 skipped), and the Native AOT publish has no warnings. The screenshots still need refreshing.
- Lower priority:
  - XFA forms;
  - tearing tabs off into windows;
  - other formats (images, comics);
  - scanning;
  - a read mode that hides the toolbar.
- Windows and macOS have only been built, not tested in use, because this environment is Linux only.

## Notes

- To publish new voice files, bump `RELEASE_TAG` in the workflow and `ReleaseTag` in `VoiceRelease.cs`, then
  re-pin the hashes. A published release is never changed.
- The dev box has about 3.5 GB of disk free.
