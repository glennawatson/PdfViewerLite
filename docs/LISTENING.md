# Listening to Read Aloud

Read Aloud uses the Kokoro-82M voice on the user's own computer. The goal is natural speech that stays restful
for long sessions, not just audio that is technically correct. This page describes how that is checked, what the
checks found, and why Kokoro stays.

## The listening corpus

`tests/listening/corpus.json` holds two kinds of material, all written for this project:

- **Cases**: about 60 short phrases, each listing the words it must and must not be read as. They cover money,
  dates (ISO, day-month and month-day), times, ordinals, decades, ranges and page references, negative numbers,
  fractions, units, abbreviations (Dr., e.g., etc., St., Fig., approx.), Roman numerals (Chapter IV, Henry VIII,
  but not the pronoun I), web and e-mail addresses, versions, symbols, citation brackets, superscript note marks and
  table of contents leaders.
- **Passages**: prose, technical writing, a paragraph dense with numbers and dates, dialogue with heavy
  punctuation, and a long session of 36 sentences (about three minutes of speech).

`ListeningCorpusTests` (in `PdfViewerLite.Core.Tests`) runs four checks against it:

| Check | What it proves |
|---|---|
| Says it as a person would | `TextNormalizer` turns every case into the expected words, in both accents |
| Knows the words | the pronunciation lexicon, with its stem and compound rules, covers each passage; at most two words in a passage may fall back to being spelled letter by letter |
| Reads cleanly | each passage, read sentence by sentence by the real voice, has no clipping, no gap inside a sentence over 1 s, and at most 0.8 s of silence at either end |
| Long session stays even | across the long session, loudness varies by at most 3 dB (standard deviation), pace by at most 20%, every pause between sentences is between 0.4 and 1.2 s, synthesis keeps ahead of playback, and the second half synthesizes no more than 1.5 times slower than the first |

Set `PDFVIEWERLITE_LISTENING_DIR` to save the long session as `long-session.wav` and hear it for yourself.

## Findings

From the run in which these checks were added (voice `af_heart`, uint8 model, CPU only):

- **Loudness** stays between -20.3 and -22.1 dBFS for every sentence of the long session. Nothing drifts, so there
  is no need to turn the volume up or down part way through.
- **Peaks** stay at or below 0.71 of full scale, so nothing clips.
- **Pace** stays between 14.6 and 21.3 letters per second of voiced speech. The slower end is very short sentences
  ("He did not need to."), which people also say more slowly.
- **Pauses**: each sentence starts after about 0.3 s and ends with about 0.45 to 0.62 s of silence, so sentences are
  about 0.8 s apart. That is within normal audiobook pacing. No sentence has an internal gap longer than 0.5 s.
- **Pronunciation**: before this work the front end only knew plain numbers, years and a handful of symbols. It had
  no rules for ordinals, money, ISO dates, clock times with a.m. and p.m., abbreviations, units, Roman numerals,
  web addresses or citation brackets, so these were read symbol by symbol or letter by letter, or read out when a
  person would skip them. `TextNormalizer` now handles all of these before the phonemizer runs, and every case in the
  corpus passes. The lexicon, with its stem and compound rules, covers each passage with at most two words spelled
  out.

## Decision

The tests show no pronunciation, cadence or listening-fatigue problem that comes from Kokoro itself. The faults
they found were in the text front end, and those are fixed. Kokoro therefore stays as the default voice; Azure AI
Speech remains an optional cloud alternative. If a future corpus case exposes a problem the front end cannot fix,
for example a stress pattern Kokoro gets wrong, that case should be added here first, before another voice is
considered.
