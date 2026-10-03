// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures rasterising one tile through PDFium into a caller owned buffer; managed allocations should be zero.</summary>
public class TileRenderBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 10;

    /// <summary>The bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The render scale (200% at 96 DPI).</summary>
    private const float Scale = 2F * 96F / 72F;

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[TileGrid.TileSize * TileGrid.TileSize * BytesPerPixel];

    /// <summary>The Calm night tone.</summary>
    private readonly PageTone _tone = new(0x2A2826U, 0xD2CDC5U);

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private IDocument _document = null!;

    /// <summary>The size of the first page, cached as the app's document source does.</summary>
    private PageSize _firstPage;

    /// <summary>Opens the document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = new PdfiumEngine().Open(_path, null);
        _firstPage = _document.GetPageSizes()[0];
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Renders the top-left tile of page 1.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderTile()
    {
        var tileSize = TileGrid.TileSize;
        var info = new PageRenderInfo(0, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        return _document.Render(info, new(_pixels, tileSize, tileSize, tileSize * BytesPerPixel));
    }

    /// <summary>Renders a tile and applies the Calm night page tone.</summary>
    /// <returns>Whether the tile rendered.</returns>
    [Benchmark]
    public bool RenderTileWithTone()
    {
        var tileSize = TileGrid.TileSize;
        var target = new RenderTarget(_pixels, tileSize, tileSize, tileSize * BytesPerPixel);
        var rendered = _document.Render(new(0, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations), target);
        _tone.Apply(target);
        return rendered;
    }

    /// <summary>
    /// Renders a whole page at preview size, as a tab hover preview or thumbnail does, then applies the page tone.
    /// The preview fits in the tile buffer, so nothing is allocated.
    /// </summary>
    /// <returns>Whether the preview rendered.</returns>
    [Benchmark]
    public bool RenderTabPreview()
    {
        var size = _firstPage;
        var scale = TileGrid.GetPreviewScale(size, PageRotation.None);
        TileGrid.GetPagePixelSize(size, PageRotation.None, scale, out var width, out var height);
        var target = new RenderTarget(_pixels, width, height, width * BytesPerPixel);
        var rendered = _document.Render(new(0, scale, PageRotation.None, 0, 0, RenderFlags.Annotations), target);
        _tone.Apply(target);
        return rendered;
    }

    /// <summary>Applies the Calm night page tone to an already rendered tile.</summary>
    [Benchmark]
    public void ApplyTone() => _tone.Apply(_pixels);
}
