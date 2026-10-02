# Speech checks

Read Aloud uses local English voices by default. Downloads need consent and are checked against saved file hashes. An optional online provider uses the user's key and receives the text it reads.

`tests/listening/corpus.json` contains short phrases and longer passages. The tests check number and date wording, abbreviations, pronunciation coverage, clipping, pauses and even pacing. Longer tests also check whether synthesis keeps ahead of playback.

Real-voice tests need the voice files. Set `PDFVIEWERLITE_LISTENING_DIR` to save the long-session audio for listening. Automated checks catch specific faults. Listening is still needed to judge natural speech.

Use the [voice tools](../tools/voice-models/README.md) to package models. Publish each update under a new release tag. Keep file sizes and hashes pinned in the app.
