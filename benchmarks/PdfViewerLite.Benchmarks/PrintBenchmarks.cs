// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures writing the print copy of a filled, annotated form; allocations come from the EventPipe trace.</summary>
public class PrintBenchmarks
{
    /// <summary>The page count the ranges are read against.</summary>
    private const int RangePageCount = 40;

    /// <summary>Where the note sits.</summary>
    private static readonly PagePoint NoteAt = new(400, 120);

    /// <summary>Two pages per sheet on A4, without annotations.</summary>
    private static readonly SheetLayout TwoPerSheet = new(2, PaperSize.A4, false);

    /// <summary>A booklet on A4, without annotations.</summary>
    private static readonly SheetLayout BookletLayout = new SheetLayout(1, PaperSize.A4, false) with { Imposition = PrintImposition.Booklet };

    /// <summary>A 2 × 2 poster on A4, without annotations.</summary>
    private static readonly SheetLayout PosterLayout = new SheetLayout(1, PaperSize.A4, false) with { Imposition = PrintImposition.Poster, PosterTiles = 2 };

    /// <summary>The page exported.</summary>
    private static readonly int[] FirstPage = [0];

    /// <summary>The chosen pages, reused.</summary>
    private readonly List<int> _pages = [with(64)];

    /// <summary>The print copy, reused so only the library's allocations are measured.</summary>
    private readonly MemoryStream _copy = new();

    /// <summary>The document.</summary>
    private OcrDocument _document = null!;

    /// <summary>The document's page exporter.</summary>
    private IPageExporter _exporter = null!;

    /// <summary>The document's editor.</summary>
    private IAnnotationEditor _editor = null!;

    /// <summary>Opens the form, fills its name field and adds a note.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _document = new(TestPdf.CreateForm());
        _editor = (IAnnotationEditor)_document.Document;
        _exporter = (IPageExporter)_document.Document;
        var filler = (IFormFiller)_document.Document;
        List<FormField> fields = [];
        filler.GetFields(0, fields);
        _ = filler.SetText(0, fields[0].Index, "Glenn Watson");
        _ = _editor.AddNote(0, NoteAt, "Checked", AnnotationColors.Sand);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _copy.Dispose();
    }

    /// <summary>Reads typed page ranges.</summary>
    /// <returns>The pages chosen.</returns>
    [Benchmark]
    public int ParsePageRanges()
    {
        _ = PageRanges.TryParse("1-3, 7, 10-", RangePageCount, _pages);
        return _pages.Count;
    }

    /// <summary>Writes the form's page two per sheet without annotations, as the print preview does.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool ExportTwoPerSheet()
    {
        _copy.Position = 0;
        _copy.SetLength(0);
        return _exporter.ExportPages(FirstPage, TwoPerSheet, _copy);
    }

    /// <summary>Writes the form's page as a booklet, padded with blank pages.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool ExportBooklet()
    {
        _copy.Position = 0;
        _copy.SetLength(0);
        return _exporter.ExportPages(FirstPage, BookletLayout, _copy);
    }

    /// <summary>Writes the form's page as a 2 × 2 poster.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool ExportPoster()
    {
        _copy.Position = 0;
        _copy.SetLength(0);
        return _exporter.ExportPages(FirstPage, PosterLayout, _copy);
    }

    /// <summary>Writes the copy handed to the print dialog.</summary>
    /// <returns>Whether it was written.</returns>
    [Benchmark]
    public bool WritePrintCopy()
    {
        _copy.Position = 0;
        _copy.SetLength(0);
        return _editor.Save(_copy);
    }
}
