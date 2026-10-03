# PdfViewerLite

A fast, tabbed PDF viewer for Linux, Windows and macOS. Every document opens as a tab in one window, so you never end
up with a hundred viewer windows. It aims for the reading features of Adobe Acrobat Reader without the cloud, while
looking at home on each desktop, and it was built for KDE Plasma first.

- **Tabs**: open many documents at once. Middle-click to close, drag to reorder, Ctrl+Tab / Ctrl+W / Ctrl+Shift+T,
  and tabs are restored on the next start. Background tabs cost almost nothing: at most 16 documents stay open in
  memory and the rest reopen when you select them.
- **Fast rendering**: [PDFium](https://pdfium.googlesource.com/pdfium/) renders 512px tiles on a dedicated thread
  straight into the bitmaps the UI draws. Work for areas you scrolled past is dropped before it reaches PDFium, and
  low-resolution previews appear instantly while sharp tiles render.
- **Desktop integration on each platform**: files opened from the file manager open as tabs in the running window,
  opened files join the platform's recent documents, *Show in Folder* opens Dolphin, Explorer or Finder, the theme
  follows the desktop live, and printing uses the platform's printer system. On KDE, KWin draws the frame, colours
  and font come from `kdeglobals`, and the open dialog is the KDE one via the XDG portal.
- **Viewer features**: continuous, page by page, dual and book (cover) layouts; fit width, fit page and free zoom
  (Ctrl+wheel zooms around the pointer); rotation; thumbnails, outline, annotations, attachments and search results
  sidebars; find with match case and whole words; text selection and copy; links; back/forward history; page labels;
  comfort page colours; document properties; password protected documents; reload when the file changes on disk;
  presentation mode; printing with a preview or through the platform's print dialog.
- **Annotate, fill and sign**: highlight, underline, strike out, notes, text boxes and drawing from context menus that
  follow what you are doing; fill in forms; draw or type a signature; list and check digital signatures; save or save
  a copy.
- **Text recognition**: *Recognise Text* gives scanned pages a searchable, selectable text layer using Tesseract.
- **Focus Mode** (Ctrl+4): the document's text reflowed in a calm reading view, with your choice of font, size, line
  spacing, paragraph spacing, text width and page colour. Two-column layouts, headings, lists, captions and footnotes
  come out in a sensible order, page numbers and running headers are left out, and switching back to the pages keeps
  your place.
- **Read Aloud**: natural sounding neural voices that run on your computer, so nothing you read is sent anywhere.
  MeloTTS is the default, with Australian, British, American and Indian English voices and BERT for natural phrasing;
  Kokoro-82M can be chosen instead. The voice is downloaded once (about 270 MB) from this project's GitHub release,
  with your go-ahead, and checked before use. It reads in the same order as
  Focus Mode; the sentence being read is softly marked and followed, with optional word highlighting and a focus band
  that dims the rest. Pause, skip a sentence, change voice or speed without losing your place, right-click *Read
  Aloud from Here* (Ctrl+Shift+Y), and pick up where you stopped next time. Azure AI Speech can be used instead with
  your own key.
- **Accessible**: every control has a name for screen readers and can be reached with the keyboard in a predictable
  order; a CI check walks the Linux AT-SPI tree of the released build.
- **Hundreds of tabs**: hover over a tab to see a preview of its page, and find any open tab by name (Ctrl+Shift+A).

- **Comfort first**: calm by default and predictable, following [comfort design rules](docs/COMFORT.md) for ADHD
  and autism. You can choose Calm, High contrast, Dark or Light (or follow the desktop), soft page colours instead
  of glaring white, text labels on the tool bar, reduced motion, a steady text cursor, and confirmation before
  closing several tabs. Everything is in Preferences (Ctrl+,).

See [docs/ACROBAT-PARITY.md](docs/ACROBAT-PARITY.md) for parity with Adobe Acrobat Reader, what is planned and what
is out of scope.

## Install

Release builds publish:

- **Linux**: an AppImage plus `.deb` and `.rpm` packages for x86_64 and aarch64; the AUR recipe lives in
  [packaging/linux/aur](packaging/linux/aur/PKGBUILD).
- **Windows**: an installer (registered for *Open with* PDF, not taking over `.pdf`) and a portable zip, for x64 and
  arm64.
- **macOS**: `PdfViewerLite.app` in a `.dmg` for Apple silicon and Intel.

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
| MVVM | [ReactiveUI 26](https://reactiveui.net), ReactiveUI.Binding and ReactiveUI.Avalonia | MIT |
| PDF engine | PDFium through our own source-generated `LibraryImport` bindings; binaries from [bblanchon/pdfium-binaries](https://github.com/bblanchon/pdfium-binaries) | BSD-3 / Apache-2.0 |
| D-Bus | [Tmds.DBus.Protocol](https://github.com/tmds/Tmds.DBus) | MIT |
| HTTP | [Refit](https://github.com/reactiveui/refit) with generated clients and source-generated JSON | MIT |
| Text recognition | Tesseract through source-generated `LibraryImport` bindings, loaded when installed | Apache-2.0 |
| Read Aloud | [MeloTTS-English](https://github.com/myshell-ai/MeloTTS) with [bert-base-uncased](https://github.com/google-research/bert) and the [g2p_en](https://github.com/Kyubyong/g2p) spelling-to-sound network, or [Kokoro-82M](https://github.com/hexgrad/kokoro) with [misaki](https://github.com/hexgrad/misaki) pronunciations, all downloaded on first use from this repository's `voices-1` release and run on [ONNX Runtime](https://onnxruntime.ai) with a C# front end; sound through PulseAudio/PipeWire, WASAPI or Core Audio | MIT / Apache-2.0 / Apache-2.0 / Apache-2.0 / Apache-2.0 / MIT / LGPL (loaded at run time) |
| Tests and benchmarks | TUnit, Avalonia.Headless, BenchmarkDotNet | MIT / Apache-2.0 |

Every shipped assembly is trimmable and Native AOT compatible; `dotnet publish` produces a single native binary with
no trim or AOT warnings. Code follows the ReactiveUI conventions: the same `.editorconfig`, central package
management, and the StyleSharp, PerformanceSharp and SecuritySharp analyzers with warnings as errors.

See [docs/ARCHITECTURE.md](docs/ARCHITECTURE.md) for how the pieces fit together.

## License

MIT, see [LICENSE](LICENSE).
