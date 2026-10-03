// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>
/// Measures creating, reading, changing and saving annotations. Each add is paired with a remove so the document
/// stays the same size between iterations. Allocations are checked from the EventPipe trace.
/// </summary>
public class AnnotationBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 4;

    /// <summary>The number of annotations on the page that is read.</summary>
    private const int ExistingAnnotations = 10;

    /// <summary>The page the annotations are read from.</summary>
    private const int ReadPage = 1;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The ink width in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The initial capacity of the save buffer, larger than the saved file.</summary>
    private const int SaveCapacity = 1 << 20;

    /// <summary>The marked lines of a two line selection.</summary>
    private static readonly PageRect[] Lines = [new(72, 100, 300, 14), new(72, 116, 220, 14)];

    /// <summary>A signature-like stroke.</summary>
    private static readonly PagePoint[] Points = [new(100, 300), new(120, 290), new(140, 310), new(160, 295), new(180, 305), new(200, 300)];

    /// <summary>The stroke lengths.</summary>
    private static readonly int[] StrokeLengths = [Points.Length];

    /// <summary>Where text is written.</summary>
    private static readonly PagePoint TextAt = new(72, 500);

    /// <summary>The list annotations are read into, reused.</summary>
    private readonly List<PageAnnotation> _annotations = [with(ExistingAnnotations)];

    /// <summary>The save buffer, reused.</summary>
    private readonly MemoryStream _saved = new(SaveCapacity);

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>Opens the document and adds the annotations that are read.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        for (var i = 0; i < ExistingAnnotations; i++)
        {
            _ = _document.AddMarkup(ReadPage, AnnotationKind.Highlight, Lines, AnnotationColors.Sand, "Existing note");
        }
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        _saved.Dispose();
        File.Delete(_path);
    }

    /// <summary>Highlights a two line selection, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddHighlight() => _document.Remove(0, _document.AddMarkup(0, AnnotationKind.Highlight, Lines, AnnotationColors.Sand, string.Empty));

    /// <summary>Adds a drawing, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddInk() => _document.Remove(0, _document.AddInk(0, Points, StrokeLengths, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink));

    /// <summary>Writes two lines of text on the page, then removes them.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddTextBox() => _document.Remove(0, _document.AddText(0, TextAt, "Approved\nGlenn", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox));

    /// <summary>Recolours an existing highlight.</summary>
    /// <returns>Whether it changed.</returns>
    [Benchmark]
    public bool Recolor() => _document.SetColor(ReadPage, 0, AnnotationColors.Sage);

    /// <summary>Reads the annotations of a page with ten highlights.</summary>
    /// <returns>The number read.</returns>
    [Benchmark]
    public int ReadAnnotations()
    {
        _annotations.Clear();
        _document.GetAnnotations(ReadPage, _annotations);
        return _annotations.Count;
    }

    /// <summary>Saves the document into a reused buffer.</summary>
    /// <returns>Whether it saved.</returns>
    [Benchmark]
    public bool Save()
    {
        _saved.Position = 0;
        _saved.SetLength(0);
        return _document.Save(_saved);
    }
}
