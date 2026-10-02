# Voice tools

Run from the repo root with the SDK in `global.json`.

```bash
dotnet run --file tools/voice-models/import_melo.cs -- tools/voice-models/melo-source.json release
dotnet run --file tools/voice-models/check_melo.cs -- tools/voice-models/melo-source.json release tests/PdfViewerLite.Core.Tests/Speech/Melo/melo-reference.json
dotnet run --file tools/voice-models/mirror_kokoro.cs -- release
dotnet run --file tools/voice-models/manifest.cs -- release
```

Kokoro imports use pinned upstream revisions. File sizes and hashes must match `VoiceRelease`. Import checks files in a staging folder before replacing output. Add a source folder as the Melo import command's third argument to use local files.

Run **Voice models** from GitHub Actions with **Run workflow**. Dispatch saves validated files as an artifact. Select **Publish** to create the pinned release. Published releases are kept unchanged. This workflow runs separately from desktop CI.

For a new model, supply trusted ONNX files and front-end data. Update source URLs, sizes and hashes in `melo-source.json`. Use a new release tag and update the app's `VoiceRelease` pins. The source release must already exist. These tools import exports; they do not convert model checkpoints.

Keep `melo-reference.json` as independent upstream evidence. To save a separate C# snapshot:

```bash
dotnet run --file tools/voice-models/reference_melo.cs -- release tests/PdfViewerLite.Core.Tests/Speech/Melo/melo-reference.json /tmp/melo-native.json
```

The tool requires a match and refuses to overwrite the reference.
