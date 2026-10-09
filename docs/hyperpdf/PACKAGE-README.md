# HyperPdfLibrary

A managed PDF engine for .NET 10 and .NET 11. It reads, renders, searches and annotates PDF files. It draws pages through SkiaSharp and works on Linux, Windows and macOS. It is trim and Native AOT safe.

HyperPdfLibrary is the engine behind [Hyper PDF Viewer](https://github.com/glennawatson/PdfViewerLite).

## Open a document and read its text

```csharp
using HyperPdfLibrary.Document;

using var document = PdfDocument.Open("input.pdf", password: null);
Console.WriteLine($"{document.PageCount} pages");
Console.WriteLine(document.GetTextPage(0).Text);
```

## Render a page

The renderer writes premultiplied BGRA pixels into a buffer you own. `Scale` is device pixels per PDF point.

```csharp
using HyperPdfLibrary.Rendering;

const float scale = 2f;
var page = document.GetPage(0);
var width = (int)MathF.Ceiling(page.Width * scale);
var height = (int)MathF.Ceiling(page.Height * scale);
var pixels = new byte[width * height * 4];

using var renderer = new PdfPageRenderer(document);
renderer.Render(new PdfTileRequest(0, scale, 0, 0, 0, PdfRenderFlags.Annotations), new PdfTileTarget(pixels, width, height, width * 4));
```

A document can be used from many render threads at once.

## Licence

HyperPdfLibrary is MIT licensed. The predefined CJK CMaps embedded in the assembly are built from Adobe's cmap-resources and mapping-resources-pdf. The notice ships in `CMaps-LICENSE.txt` in this package.

Report problems at [GitHub issues](https://github.com/glennawatson/PdfViewerLite/issues).
