# HyperPdfLibrary

A managed PDF engine for .NET 10 and .NET 11. It reads, renders, searches and annotates PDF files. It draws pages through SkiaSharp and works on Linux, Windows and macOS. It is trim and Native AOT safe.

HyperPdfLibrary is the engine behind [Hyper PDF Viewer](https://github.com/glennawatson/PdfViewerLite).

## Open a document and read its text

```csharp
using HyperPdfLibrary.Document;

using var document = await PdfDocumentReader.OpenAsync("input.pdf", password: null, CancellationToken.None);
Console.WriteLine($"{document.PageCount} pages");
Console.WriteLine((await PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None)).Text);
```

## Render a page

The renderer writes premultiplied BGRA pixels into a buffer you own. `Scale` is device pixels per PDF point.

```csharp
using HyperPdfLibrary.Rendering;

const float scale = 2f;
var page = PdfDocumentPages.GetPage(document, 0);
var width = (int)MathF.Ceiling(page.Width * scale);
var height = (int)MathF.Ceiling(page.Height * scale);
var pixels = new byte[width * height * 4];

using var renderer = new PdfPageRenderer(document);
await renderer.RenderAsync(new PdfTileRequest(0, scale, 0, 0, 0, PdfRenderFlags.Annotations), pixels, width, height, width * 4, CancellationToken.None);
```

A document can be used from many render threads at once.

## Font data on demand

The engine generates predefined CJK mappings and substitute faces only when a requested page needs them. It reads compact binary CMap resources and real TrueType fonts from pinned resource packs, builds the needed engine tables, and caches only the requested assets under `HyperPdfLibrary/FontData/v3` in the user's local application data folder.

Use `PdfDocumentText.GetTextPageAsync`, `PdfDocumentPages.PrefetchPageAsync`, `PdfFontLoader.LoadAsync` or the renderer's `RenderAsync` for first use. These await missing font data before the synchronous core runs. The synchronous APIs use cached resources. Later accesses reuse the parsed data in memory; later runs reuse the generated files without downloading them again. The first request for an uncached resource needs internet access. Set `HYPERPDF_FONT_DATA` before first use to choose a different cache root.

Identity CMaps and fonts embedded in the PDF need no substitute font downloads. PDFs with complete embedded CMaps and ToUnicode mappings need no external CMap data.

## Licence

HyperPdfLibrary is MIT licensed. CJK mappings use the PDF.js binary CMap resource pack under Adobe's BSD licence. Substitute faces use the Google Fonts pack: Arimo, Tinos, Cousine and Noto symbols. Their Apache and SIL notices ship in this package and are copied alongside cached fonts.

Report problems at [GitHub issues](https://github.com/glennawatson/PdfViewerLite/issues).
