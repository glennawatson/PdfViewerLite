// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for rectangles, ellipses, arrows, lines and stamps: they are read back as what they are, drawn on the page, and survive saving.</summary>
public sealed class ShapeAnnotationTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>The page the shapes go on.</summary>
    private const int Page = 1;

    /// <summary>The line width in points.</summary>
    private const float Width = 3;

    /// <summary>Bytes per rendered pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Halves a sum to find a midpoint.</summary>
    private const float Half = 0.5F;

    /// <summary>A white channel.</summary>
    private const byte White = 0xFF;

    /// <summary>The rectangle's top-left corner.</summary>
    private static readonly PagePoint BoxStart = new(72, 100);

    /// <summary>The rectangle's bottom-right corner.</summary>
    private static readonly PagePoint BoxEnd = new(200, 180);

    /// <summary>The ellipse's top-left corner.</summary>
    private static readonly PagePoint OvalStart = new(300, 100);

    /// <summary>The ellipse's bottom-right corner.</summary>
    private static readonly PagePoint OvalEnd = new(420, 180);

    /// <summary>The arrow's tail.</summary>
    private static readonly PagePoint Tail = new(72, 300);

    /// <summary>The arrow's point.</summary>
    private static readonly PagePoint Point = new(272, 300);

    /// <summary>The line's start.</summary>
    private static readonly PagePoint LineStart = new(72, 400);

    /// <summary>The line's end.</summary>
    private static readonly PagePoint LineEnd = new(272, 440);

    /// <summary>Where the stamp goes.</summary>
    private static readonly PagePoint StampAt = new(300, 500);

    /// <summary>Each shape and the stamp is read back as what it is, with its colour.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAndReadsEachShape()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;

        var indexes = AddAll(editor);
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(Page, annotations);

        AnnotationKind[] expected = [AnnotationKind.Rectangle, AnnotationKind.Ellipse, AnnotationKind.Arrow, AnnotationKind.Line, AnnotationKind.Stamp];
        await Assert.That(Array.TrueForAll(indexes, static i => i >= 0)).IsTrue();
        await Assert.That(annotations.Select(static a => a.Kind).ToArray()).IsEquivalentTo(expected);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Rectangle).Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Stamp).Contents).IsEqualTo("APPROVED");
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.Rectangle).Bounds.Left).IsLessThanOrEqualTo(BoxStart.X);
    }

    /// <summary>A zero-length arrow, an empty stamp and an unknown shape are refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesEmptyShapes()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;

        var arrow = editor.AddShape(Page, AnnotationKind.Arrow, Tail, Tail, AnnotationColors.Ink, Width);
        var stamp = editor.AddStamp(Page, StampAt, " ", AnnotationColors.Clay);
        var other = editor.AddShape(Page, AnnotationKind.Highlight, Tail, Point, AnnotationColors.Ink, Width);

        await Assert.That(arrow).IsEqualTo(-1);
        await Assert.That(stamp).IsEqualTo(-1);
        await Assert.That(other).IsEqualTo(-1);
    }

    /// <summary>The shapes are drawn on the page and survive saving and reopening, recoloured stamps included.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DrawsAndSaves()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var indexes = AddAll(editor);
        var recoloured = editor.SetColor(Page, indexes[^1], AnnotationColors.Slate);
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-shapes-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(path))
            {
                _ = editor.Save(stream);
            }

            using var reopened = new PdfiumEngine().Open(path, null);
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!).GetAnnotations(Page, annotations);
            var pixels = Render(reopened, out var width);

            await Assert.That(recoloured).IsTrue();
            await Assert.That(annotations.Count).IsEqualTo(indexes.Length);
            await Assert.That(IsInked(pixels, width, new(BoxStart.X, (BoxStart.Y + BoxEnd.Y) * Half))).IsTrue();
            await Assert.That(IsInked(pixels, width, new(OvalStart.X + 1, (OvalStart.Y + OvalEnd.Y) * Half))).IsTrue();
            await Assert.That(IsInked(pixels, width, new((Tail.X + Point.X) * Half, Tail.Y))).IsTrue();
            await Assert.That(IsInked(pixels, width, new((LineStart.X + LineEnd.X) * Half, (LineStart.Y + LineEnd.Y) * Half))).IsTrue();
            await Assert.That(IsInked(pixels, width, new(StampAt.X + 1, StampAt.Y + Width))).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Adds every shape and a stamp.</summary>
    /// <param name="editor">The editor.</param>
    /// <returns>The new annotations' indexes.</returns>
    private static int[] AddAll(IAnnotationEditor editor) =>
    [
        editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width),
        editor.AddShape(Page, AnnotationKind.Ellipse, OvalStart, OvalEnd, AnnotationColors.Slate, Width),
        editor.AddShape(Page, AnnotationKind.Arrow, Tail, Point, AnnotationColors.Ink, Width),
        editor.AddShape(Page, AnnotationKind.Line, LineStart, LineEnd, AnnotationColors.Sage, Width),
        editor.AddStamp(Page, StampAt, "APPROVED", AnnotationColors.Clay),
    ];

    /// <summary>Renders the shapes' page at one pixel per point.</summary>
    /// <param name="document">The document.</param>
    /// <param name="width">The page width in pixels.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Render(IDocument document, out int width)
    {
        TestPdf.GetPageSize(Page, out width, out var height);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = document.Render(new(Page, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
        return pixels;
    }

    /// <summary>Determines whether any pixel within a point or so of a page point is not white.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="width">The page width in pixels.</param>
    /// <param name="point">The page point.</param>
    /// <returns><see langword="true"/> when something is drawn there.</returns>
    private static bool IsInked(byte[] pixels, int width, PagePoint point)
    {
        for (var dy = -1; dy <= 1; dy++)
        {
            for (var dx = -1; dx <= 1; dx++)
            {
                var offset = ((((int)point.Y + dy) * width) + (int)point.X + dx) * BytesPerPixel;
                if (pixels.AsSpan(offset, BytesPerPixel - 1).ContainsAnyExcept(White))
                {
                    return true;
                }
            }
        }

        return false;
    }
}
