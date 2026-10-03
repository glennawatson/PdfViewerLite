# Listening to Read Aloud

Read Aloud uses a neural voice on the user's own computer. The goal is natural speech that stays restful for long
sessions, not just audio that is technically correct. This page describes how that is checked, what the checks found,
and why MeloTTS is the default with Kokoro as a choice.

## Voices

| Engine | Voices | Where it runs | Notes |
|---|---|---|---|
| MeloTTS-English (default) | Australian, British, American, Indian English | On this computer, ONNX Runtime | BERT (bert-base-uncased, first ten layers, int8) supplies sentence-level phrasing; 44.1 kHz |
| Kokoro-82M | American and British English | On this computer, ONNX Runtime | 24 kHz |
| Azure AI Speech | Many | Azure, with the user's own key | Optional |

Both on-device voices are downloaded once from this repository's `voices-1` GitHub release, built by
`.github/workflows/voice-models.yml`, and each file is checked against a SHA-256 pinned in `VoiceRelease.cs`. Python is
used only in that workflow to export the models; the app's MeloTTS front end (BERT word pieces, the CMU dictionary,
g2p_en's spelling-to-sound network, phone-to-token alignment) is C#, and `MeloFrontEndTests` checks it produces exactly
what the Python original does.

## Why MeloTTS became the default

Kokoro passed every check below, but it has no Australian voice. The voice had to run on device and include
Australian English, without any Python at run time. MeloTTS-English has an Australian speaker, an MIT licence, and
exports cleanly to ONNX: the exported synthesizer matches PyTorch to within 3e-5 per sample, the int8 BERT keeps a mean
cosine of 0.985 to the full model, and synthesis runs about three times faster than playback on four CPU threads.
MeloTTS ends each reading with about 0.8 s of silence; the engine cuts that to 0.45 s so the pauses between sentences
stay even.

## The listening corpus

`tests/listening/corpus.json` holds two kinds of material, all written for this project:

- **Cases**: about 60 short phrases, each listing the words it must and must not be read as. They cover money,
  dates (ISO, day-month and month-day), times, ordinals, decades, ranges and page references, negative numbers,
  fractions, units, abbreviations (Dr., e.g., etc., St., Fig., approx.), Roman numerals (Chapter IV, Henry VIII,
  but not the pronoun I), web and e-mail addresses, versions, symbols, citation brackets, superscript note marks and
  table of contents leaders.
- **Passages**: prose, technical writing, a paragraph dense with numbers and dates, dialogue with heavy
  punctuation, and a long session of 36 sentences (about three minutes of speech).

`ListeningCorpusTests` (in `PdfViewerLite.Core.Tests`) runs these checks, for both on-device voices:

| Check | What it proves |
|---|---|
| Says it as a person would | `TextNormalizer` turns every case into the expected words, in both accents |
| Knows the words | the dictionary covers each passage; at most two words in a passage may need guessing |
| Reads cleanly | each passage, read sentence by sentence by the real voice, has no clipping, no gap inside a sentence over 1 s, and at most 0.8 s of silence at either end |
| Long session stays even | across the long session, loudness varies by at most 3 dB (standard deviation), pace by at most 20%, every pause between sentences is between 0.4 and 1.2 s, synthesis keeps ahead of playback, and the second half synthesizes no more than 1.5 times slower than the first |

Set `PDFVIEWERLITE_LISTENING_DIR` to save the long sessions as `long-session.wav` (Kokoro) and
`long-session-melo.wav` (MeloTTS, Australian) and hear them for yourself.

## Findings

Kokoro (voice `af_heart`, uint8 model, CPU only):

- **Loudness** stays between -20.3 and -22.1 dBFS for every sentence of the long session.
- **Peaks** stay at or below 0.71 of full scale, so nothing clips.
- **Pace** stays between 14.6 and 21.3 letters per second of voiced speech.
- **Pauses**: sentences are about 0.8 s apart, within normal audiobook pacing.

MeloTTS (voice `EN-AU`, BERT on, CPU only):

- **Loudness** stays around -19 dBFS; peaks stay below 0.75 of full scale.
- **Pace** stays between about 16 and 21 letters per second.
- **Pauses**: about 0.1 s before each sentence and 0.45 s after it once trimmed, plus Read Aloud's own gap.
- Every passage and the long session pass all checks.

The front end faults the corpus first found (numbers, dates, abbreviations, units, Roman numerals, addresses,
citations) were in the text normalizer, which both voices share, and are fixed.
