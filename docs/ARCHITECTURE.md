# Architecture and checks

| Project | Purpose |
|---|---|
| Core | Documents, layout, rendering, search, settings and signing |
| Pdfium | PDF engine and native bindings |
| App | Avalonia views and ReactiveUI view models |
| Platform projects | Printing, themes, recent files and desktop integration |
| Http, Ocr, Speech | Downloads, text recognition and voices |
| tests, benchmarks, tools | Verification and C# development tools |

`DocumentPool` opens documents on demand. It keeps at most 16 open by default. Page coordinates use points from the top-left corner. The engine handles crop boxes and stored rotation. Every PDFium call holds `PdfiumLibrary.EnterScope()`.

`PageCanvas` draws a preview, then visible tiles. `RenderScheduler` renders tiles on one thread. It drops stale requests. `RenderHub` passes completed tiles to the bounded `TileCache`. Page colours are applied during rendering.

Views use generated bindings and dispose subscriptions when unloaded. Notifications use `IObservable<T>`. Dialogs use ReactiveUI interactions. `AppServices` creates services and applies settings. Shipped code must remain trim and Native AOT safe.

Build and test commands are in the [README](../README.md#build). For focused checks:

```bash
dotnet run -c Release --project benchmarks/PdfViewerLite.Benchmarks -- --filter '*'
scripts/audit-allocations.sh '*TileRender*'
scripts/check-screen-reader.sh artifacts/linux-x64/pdfviewerlite
```

Allocation checks use EventPipe traces and `benchmarks/allocations-explained.json`. UI tests and benchmarks run headless. Set `PDFVIEWERLITE_SCREENSHOTS` to save UI frames. Add `--wayland` to the screen-reader script for a private Wayland compositor.

See [development rules](../AGENTS.md), [voice tools](../tools/voice-models/README.md) and [framework access issues](upstream/avalonia/README.md).
