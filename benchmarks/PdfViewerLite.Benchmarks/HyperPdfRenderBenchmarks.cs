// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Compares rendering one tile with PDFium and HyperPDF. The warm benchmarks render a page that is already open and, for
/// HyperPDF, already recorded to a picture. The cold benchmarks open the file and render its first tile. The text page is
/// not drawn by HyperPDF until fonts are native, so its HyperPDF times leave out the text.
/// </summary>
public class HyperPdfRenderBenchmarks
{
    /// <summary>The page count of the text document.</summary>
    private const int TextPages = 3;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The render scale (200% at 96 DPI).</summary>
    private const float Scale = 2F * 96F / 72F;

    /// <summary>The size of the generated scan image.</summary>
    private const int ScanSize = 256;

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[TileGrid.TileSize * TileGrid.TileSize * BytesPerPixel];

    /// <summary>The PDFium engine.</summary>
    private readonly PdfiumEngine _pdfium = new();

    /// <summary>The HyperPDF engine.</summary>
    private readonly HyperPdfEngine _hyperPdf = new();

    /// <summary>The document on disk.</summary>
    private string _path = string.Empty;

    /// <summary>The document opened with PDFium.</summary>
    private IDocument _pdfiumDocument = null!;

    /// <summary>The document opened with HyperPDF.</summary>
    private IDocument _hyperPdfDocument = null!;

    /// <summary>Gets or sets which sample document is rendered: Text, Layers, Scan, Transparency or Annotations.</summary>
    [Params("Text", "Layers", "Scan", "Transparency", "Annotations")]
    public string Document { get; set; } = "Text";

    /// <summary>Writes the document and opens it with both engines, rendering once so caches are warm.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-render-bench-{Environment.ProcessId}-{Document}.pdf");
        File.WriteAllBytes(_path, CreateDocument(Document));
        _pdfiumDocument = _pdfium.Open(_path, null);
        _hyperPdfDocument = _hyperPdf.Open(_path, null);
        _ = RenderTile(_pdfiumDocument);
        _ = RenderTile(_hyperPdfDocument);
    }

    /// <summary>Closes the documents and deletes the file.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _pdfiumDocument.Dispose();
        _hyperPdfDocument.Dispose();
        File.Delete(_path);
    }

    /// <summary>Renders the top-left tile of page 1 with PDFium.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool PdfiumWarmTile() => RenderTile(_pdfiumDocument);

    /// <summary>Renders the top-left tile of page 1 with HyperPDF, replaying the recorded picture.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool HyperPdfWarmTile() => RenderTile(_hyperPdfDocument);

    /// <summary>Opens the file with PDFium and renders the first tile.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool PdfiumColdTile() => OpenAndRender(_pdfium);

    /// <summary>Opens the file with HyperPDF and renders the first tile, which records the page.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool HyperPdfColdTile() => OpenAndRender(_hyperPdf);

    /// <summary>Creates a sample document.</summary>
    /// <param name="name">The document name.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] CreateDocument(string name)
    {
        if (name == "Layers")
        {
            return TestPdf.CreateWithLayers();
        }

        if (name == "Transparency")
        {
            return RenderSamplePages.CreateTransparencyPage();
        }

        if (name == "Annotations")
        {
            return RenderSamplePages.CreateAnnotationPage();
        }

        if (name != "Scan")
        {
            return TestPdf.Create(TextPages);
        }

        var grey = new byte[ScanSize * ScanSize];
        for (var i = 0; i < grey.Length; i++)
        {
            grey[i] = (byte)(i % byte.MaxValue);
        }

        return TestPdf.CreateScan(grey, ScanSize, ScanSize);
    }

    /// <summary>Renders the top-left tile of page 1.</summary>
    /// <param name="document">The document.</param>
    /// <returns>Whether the tile rendered.</returns>
    private bool RenderTile(IDocument document)
    {
        var tileSize = TileGrid.TileSize;
        var info = new PageRenderInfo(0, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        return document.Render(info, new(_pixels, tileSize, tileSize, tileSize * BytesPerPixel));
    }

    /// <summary>Opens the file and renders the first tile.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>Whether the tile rendered.</returns>
    private bool OpenAndRender(IDocumentEngine engine)
    {
        using var document = engine.Open(_path, null);
        return RenderTile(document);
    }
}
