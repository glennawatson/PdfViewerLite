// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.HyperPdf;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Compares opening a document and reading its navigation with PDFium and HyperPDF. Each benchmark opens a fresh
/// document, so caches start empty, as when a file is opened in the app.
/// </summary>
public class HyperPdfDocumentBenchmarks
{
    /// <summary>The pages in the sample document.</summary>
    private const int Pages = 50;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The line highlighted on page 1.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>The PDFium engine.</summary>
    private readonly PdfiumEngine _pdfium = new();

    /// <summary>The pixels page 1 is drawn into, large enough for a portrait or landscape letter page.</summary>
    private readonly byte[] _pixels = new byte[TestPdf.PortraitWidth * TestPdf.PortraitHeight * BytesPerPixel];

    /// <summary>The stream saves are written to, reused so each save measures only the engine.</summary>
    private readonly MemoryStream _saved = new();

    /// <summary>The HyperPDF engine.</summary>
    private readonly HyperPdfEngine _hyperPdf = new();

    /// <summary>The sample document on disk.</summary>
    private string _path = string.Empty;

    /// <summary>Writes the sample document.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = Path.Combine(Path.GetTempPath(), $"hyperpdf-bench-{Environment.ProcessId}.pdf");
        File.WriteAllBytes(_path, TestPdf.Create(Pages));
    }

    /// <summary>Deletes the sample document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        File.Delete(_path);
        _saved.Dispose();
    }

    /// <summary>Opens the document with PDFium and reads every page size.</summary>
    /// <returns>The page count.</returns>
    [Benchmark(Baseline = true)]
    public int PdfiumOpen() => OpenAndMeasure(_pdfium);

    /// <summary>Opens the document with HyperPDF and reads every page size.</summary>
    /// <returns>The page count.</returns>
    [Benchmark]
    public int HyperPdfOpen() => OpenAndMeasure(_hyperPdf);

    /// <summary>Opens the document with PDFium and reads its outline, labels and first page's links.</summary>
    /// <returns>A count, so the work is not optimised away.</returns>
    [Benchmark]
    public int PdfiumNavigation() => OpenAndNavigate(_pdfium);

    /// <summary>Opens the document with HyperPDF and reads its outline, labels and first page's links.</summary>
    /// <returns>A count, so the work is not optimised away.</returns>
    [Benchmark]
    public int HyperPdfNavigation() => OpenAndNavigate(_hyperPdf);

    /// <summary>Opens the document with PDFium, draws and reads page 1, highlights a line and saves.</summary>
    /// <returns>A count, so the work is not optimised away.</returns>
    [Benchmark]
    public int PdfiumEndToEnd() => EndToEnd(_pdfium);

    /// <summary>Opens the document with HyperPDF, draws and reads page 1, highlights a line and saves.</summary>
    /// <returns>A count, so the work is not optimised away.</returns>
    [Benchmark]
    public int HyperPdfEndToEnd() => EndToEnd(_hyperPdf);

    /// <summary>Opens a document and reads its page sizes.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>The page count.</returns>
    private int OpenAndMeasure(IDocumentEngine engine)
    {
        using var document = engine.Open(_path, null);
        return document.GetPageSizes().Length;
    }

    /// <summary>Opens a document and reads its navigation.</summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A count.</returns>
    private int OpenAndNavigate(IDocumentEngine engine)
    {
        using var document = engine.Open(_path, null);
        var count = document.GetOutline().Count + document.GetLinks(0).Count;
        for (var i = 0; i < document.PageCount; i++)
        {
            count += document.GetPageLabel(i)?.Length ?? 0;
        }

        return count;
    }

    /// <summary>
    /// The first minutes of a review: open, draw page 1 at one pixel per point, read its text, add a highlight and save
    /// the result into memory.
    /// </summary>
    /// <param name="engine">The engine.</param>
    /// <returns>A count.</returns>
    private int EndToEnd(IDocumentEngine engine)
    {
        using var document = engine.Open(_path, null);
        var size = document.GetPageSizes()[0];
        var width = (int)size.Width;
        var height = (int)size.Height;
        var drawn = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(_pixels, width, height, width * BytesPerPixel));
        var text = document.GetText(0, 0, document.GetCharacterCount(0));
        var editor = (IAnnotationEditor)document;
        var index = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
        _saved.SetLength(0);
        var saved = editor.Save(_saved);
        return (drawn ? 1 : 0) + text.Length + index + (saved ? (int)_saved.Length : 0);
    }
}
