// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Benchmarks;

/// <summary>Measures editing placed comments: moving, resizing, restyling, undo and redo, and the newer shapes.</summary>
public class AnnotationEditingBenchmarks
{
    /// <summary>The page count of the generated document.</summary>
    private const int DocumentPages = 2;

    /// <summary>The ink width in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The wider ink width in points.</summary>
    private const float WideInk = 4;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>Where the rectangle starts.</summary>
    private static readonly PageRect Start = new(100, 100, 120, 60);

    /// <summary>Where the rectangle is moved to.</summary>
    private static readonly PageRect Moved = new(140, 160, 120, 60);

    /// <summary>A five sided outline.</summary>
    private static readonly PagePoint[] Vertices = [new(100, 400), new(200, 380), new(240, 460), new(160, 520), new(90, 480)];

    /// <summary>The history moves are recorded in.</summary>
    private readonly AnnotationHistory _history = new();

    /// <summary>The temporary file.</summary>
    private string _path = string.Empty;

    /// <summary>The document.</summary>
    private PdfiumDocument _document = null!;

    /// <summary>The rectangle that is edited.</summary>
    private int _rectangle;

    /// <summary>Opens the document and draws the rectangle that is edited.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _path = TestPdf.WriteTempFile(DocumentPages);
        _document = (PdfiumDocument)new PdfiumEngine().Open(_path, null);
        _rectangle = _document.AddShape(0, AnnotationKind.Rectangle, new(Start.Left, Start.Top), new(Start.Left + Start.Width, Start.Top + Start.Height), AnnotationColors.Ink, InkWidth);
        _history.RecordMove(0, _rectangle, Start, Moved, false);
    }

    /// <summary>Closes the document.</summary>
    [GlobalCleanup]
    public void Cleanup()
    {
        _document.Dispose();
        File.Delete(_path);
    }

    /// <summary>Moves the rectangle and back again.</summary>
    /// <returns>Whether both moves were kept.</returns>
    [Benchmark]
    public bool MoveAndBack() => _document.SetBounds(0, _rectangle, Moved) && _document.SetBounds(0, _rectangle, Start);

    /// <summary>Undoes a move and makes it again.</summary>
    /// <returns>Whether both steps ran.</returns>
    [Benchmark]
    public bool UndoRedoMove() => _history.Undo(_document) is not null && _history.Redo(_document) is not null;

    /// <summary>Widens the rectangle's line and narrows it again.</summary>
    /// <returns>Whether both changes were kept.</returns>
    [Benchmark]
    public bool RestyleLine() => _document.SetLineWidth(0, _rectangle, WideInk) && _document.SetLineWidth(0, _rectangle, InkWidth);

    /// <summary>Draws a cloud, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddCloud() => _document.Remove(1, _document.AddPolygon(1, AnnotationKind.Cloud, Vertices, AnnotationColors.Ink, InkWidth));

    /// <summary>Places a callout, then removes it.</summary>
    /// <returns>Whether it was removed.</returns>
    [Benchmark]
    public bool AddCallout() => _document.Remove(1, _document.AddCallout(1, Vertices[0], Vertices[2], "Check this", FontSize, AnnotationColors.Ink));
}
