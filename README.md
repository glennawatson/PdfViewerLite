# PdfViewerLite

A fast, tabbed PDF viewer for Linux, built for KDE Plasma first. Every document opens as a tab in one window, so you
never end up with a hundred viewer windows. It aims for the feature set of GNOME's Papers ("Document Viewer") and
the tabbed workflow of Adobe Reader, while looking at home on KDE.

- **Tabs**: open many documents at once. Middle-click to close, drag to reorder, Ctrl+Tab / Ctrl+W / Ctrl+Shift+T,
  and tabs are restored on the next start. Background tabs cost almost nothing: at most 16 documents stay open in
  memory and the rest reopen when you select them.
- **Fast rendering**: [PDFium](https://pdfium.googlesource.com/pdfium/) renders 512px tiles on a dedicated thread
  straight into the bitmaps the UI draws. Work for areas you scrolled past is dropped before it reaches PDFium, and
  low-resolution previews appear instantly while sharp tiles render.
- **KDE integration**: KWin draws the window frame, the window content follows your colour scheme and font from
  `kdeglobals` (live), files opened from Dolphin open as tabs in the running window over D-Bus, opened files show in
  Dolphin's *Recent Files*, the open dialog is the KDE one (via the XDG portal), and *Show in Folder* opens Dolphin.
- **Viewer features**: continuous single, dual and book (cover) layouts; fit width, fit page and free zoom
  (Ctrl+wheel zooms around the pointer); rotation; thumbnails, outline and search results sidebar; find with match
  case and whole words; text selection and copy; links; back/forward history; page labels; night mode; document
  properties; password protected documents; reload when the file changes on disk.

See [docs/FEATURES.md](docs/FEATURES.md) for parity with GNOME Papers 50 and what is planned.

## Install

Release builds publish an AppImage plus `.deb` and `.rpm` packages for x86_64 and aarch64, and the AUR recipe lives
in [packaging/linux/aur](packaging/linux/aur/PKGBUILD).

To build and install from source for the current user:

```bash
scripts/publish-linux.sh linux-x64        # Native AOT build into artifacts/linux-x64
packaging/linux/install.sh                # installs into ~/.local (use --prefix for elsewhere)
xdg-mime default net.glennwatson.PdfViewerLite.desktop application/pdf
```

Building needs the .NET 10 SDK and, for Native AOT, `clang` and `zlib` development files.

## Build and test

```bash
dotnet build PdfViewerLite.slnx
dotnet test --solution PdfViewerLite.slnx
dotnet run --project src/PdfViewerLite.App -- some.pdf
dotnet run -c Release --project benchmarks/PdfViewerLite.Benchmarks -- --filter '*'
```

Tests use TUnit on Microsoft.Testing.Platform. UI tests run headlessly with Avalonia.Headless and real Skia rendering;
set `PDFVIEWERLITE_SCREENSHOTS=<dir>` to save the rendered frames.

## Technology

| Concern | Choice | License |
|---|---|---|
| UI | [Avalonia 12](https://avaloniaui.net) with compiled bindings | MIT |
| MVVM | [ReactiveUI 25](https://reactiveui.net) and ReactiveUI.Avalonia | MIT |
| PDF engine | PDFium through our own source-generated `LibraryImport` bindings; binaries from [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) | BSD-3 / Apache-2.0 |
| D-Bus | [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) | MIT |
| HTTP | [Refit](https://github.com/reactiveui/refit) with generated clients and source-generated JSON | MIT |
| Tests and benchmarks | TUnit, Avalonia.Headless, BenchmarkDotNet | MIT / Apache-2.0 |

Every shipped assembly is trimmable and Native AOT compatible; `dotnet publish` produces a single native binary with
no trim or AOT warnings. Code follows the ReactiveUI conventions: the same `.editorconfig`, central package
management, and the StyleSharp, PerformanceSharp and SecuritySharp analyzers with warnings as errors.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the pieces fit together.

## License

MIT, see [LICENSE](LICENSE).
