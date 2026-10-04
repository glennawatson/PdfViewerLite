# Voice tools

Run from the repo root with the SDK in `global.json`.

```bash
dotnet run --file tools/voice-models/import_melo.cs -- tools/voice-models/melo-source.json release
dotnet run --file tools/voice-models/check_melo.cs -- tools/voice-models/melo-source.json release tests/PdfViewerLite.Core.Tests/Speech/Melo/melo-reference.json
tools/voice-models/mirror_kokoro.sh release
dotnet run --file tools/voice-models/manifest.cs -- release
```

Before mirroring, set `KOKORO_ONNX` and `MISAKI` to the pinned workflow URLs. Import checks files in a staging folder before replacing output. Add a source folder as the import command's third argument to use local files.

For a new model, supply trusted ONNX files and front-end data. Update source URLs, sizes and hashes in `melo-source.json`. Use a new release tag and update the app's `VoiceRelease` pins. The source release must already exist. These tools import exports; they do not convert model checkpoints.

Keep `melo-reference.json` as independent upstream evidence. To save a separate C# snapshot:

```bash
dotnet run --file tools/voice-models/reference_melo.cs -- release tests/PdfViewerLite.Core.Tests/Speech/Melo/melo-reference.json /tmp/melo-native.json
```

The tool requires a match and refuses to overwrite the reference.
