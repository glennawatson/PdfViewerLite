// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Graphics.Images;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Renders pages of two scanned court reports (a JBIG2 book from Google Books and a JPEG 2000 book from the Internet
/// Archive) with HyperPDF and PDFium, each call drawing the next page so no page is warm. Also times decoding one page's
/// image to BGRA and to the compact gray form the renderer uses. Does nothing when the corpus file is not cached.
/// </summary>
public class HyperPdfScanBenchmarks
{
    /// <summary>The tile size in pixels.</summary>
    private const int Tile = 512;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The render scale: 150% at 96 DPI.</summary>
    private const float Scale = 1.5F * 96F / 72F;

    /// <summary>The page whose image the decode benchmarks use.</summary>
    private const int DecodedPage = 40;

    /// <summary>The tile pixels.</summary>
    private readonly byte[] _pixels = new byte[Tile * Tile * BytesPerPixel];

    /// <summary>The PDFium engine.</summary>
    private readonly PdfiumEngine _pdfium = new();

    /// <summary>The managed document, or null when the file is not cached.</summary>
    private PdfDocument? _document;

    /// <summary>The managed renderer.</summary>
    private PdfPageRenderer? _renderer;

    /// <summary>The PDFium document, or null when the file is not cached.</summary>
    private IDocument? _pdfiumDocument;

    /// <summary>The image of <see cref="DecodedPage"/>.</summary>
    private PdfStream? _image;

    /// <summary>The next page HyperPDF draws.</summary>
    private int _next;

    /// <summary>The next page PDFium draws.</summary>
    private int _nextPdfium;

    /// <summary>Gets or sets the corpus file id.</summary>
    [Params("google-us-reports-supreme-court", "ia-us-reports-341")]
    public string CorpusId { get; set; } = "google-us-reports-supreme-court";

    /// <summary>Opens the corpus file with both engines.</summary>
    [GlobalSetup]
    public void Setup()
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus", $"{CorpusId}.pdf");
        if (!File.Exists(path))
        {
            return;
        }

        _document = PdfDocumentReader.Open(path, null);
        _renderer = new(_document);
        _pdfiumDocument = _pdfium.Open(path, null);
        _image = FirstImage(PdfDocumentPages.GetPage(_document, DecodedPage));
    }

    /// <summary>Closes the documents.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _renderer?.Dispose();
        _document?.Dispose();
        _pdfiumDocument?.Dispose();
    }

    /// <summary>Renders the next page with PDFium.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark(Baseline = true)]
    public bool PdfiumNextPage()
    {
        if (_pdfiumDocument is null)
        {
            return false;
        }

        _nextPdfium = (_nextPdfium + 1) % _pdfiumDocument.PageCount;
        PageRenderInfo info = new(_nextPdfium, Scale, PageRotation.None, 0, 0, RenderFlags.Annotations);
        return _pdfiumDocument.Render(info, new(_pixels, Tile, Tile, Tile * BytesPerPixel));
    }

    /// <summary>Renders the next page with HyperPDF: decode the image, record the page, draw one tile.</summary>
    /// <returns>Whether the page rendered.</returns>
    [Benchmark]
    public bool HyperPdfNextPage()
    {
        if (_document is null || _renderer is null)
        {
            return false;
        }

        _next = (_next + 1) % _document.PageCount;
        PdfTileRequest request = new(_next, Scale, 0, 0, 0, PdfRenderFlags.Annotations);
        return _renderer.Render(request, new(_pixels, Tile, Tile, Tile * BytesPerPixel));
    }

    /// <summary>Decodes one page image to BGRA, as the public decoder does.</summary>
    /// <returns>The pixel bytes.</returns>
    [Benchmark]
    public int DecodeToBgra() => _image is null ? 0 : PdfImageDecoder.Decode(_image)?.Pixels.Length ?? 0;

    /// <summary>Decodes one page image to the compact form the renderer draws: gray bytes for greyscale and bilevel images.</summary>
    /// <returns>The pixel bytes.</returns>
    [Benchmark]
    public int DecodeCompact() => _image is null ? 0 : PdfImageDecoder.DecodeCompact(_image)?.Pixels.Length ?? 0;

    /// <summary>Finds the first image XObject of a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The image stream, or <see langword="null"/>.</returns>
    private static PdfStream? FirstImage(PdfPage page)
    {
        var xobjects = page.Resources?.GetDictionary(KnownName.XObject);
        for (var i = 0; i < (xobjects?.Count ?? 0); i++)
        {
            if (xobjects!.Get(xobjects.GetKeyAt(i)).AsStream() is { } stream && stream.Dictionary.IsName(KnownName.Subtype, KnownName.Image))
            {
                return stream;
            }
        }

        return null;
    }
}
