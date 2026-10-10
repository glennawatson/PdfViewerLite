// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for annotation editing and saving through <see cref="IAnnotationEditor"/>.</summary>
public sealed class AnnotationTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>Tolerance for positions, in points.</summary>
    private const float Tolerance = 2F;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The typed signature size in points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The ink line width in points.</summary>
    private const float InkWidth = 2;

    /// <summary>The marked line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>A drawn stroke.</summary>
    private static readonly PagePoint[] Stroke = [new(100, 300), new(140, 320), new(180, 300), new(220, 330)];

    /// <summary>Where the sticky note goes.</summary>
    private static readonly PagePoint NoteAt = new(400, 100);

    /// <summary>Where the text box goes.</summary>
    private static readonly PagePoint TextAt = new(72, 500);

    /// <summary>Where the typed signature goes.</summary>
    private static readonly PagePoint SignatureAt = new(72, 600);

    /// <summary>Verifies each kind can be added and is read back with its kind, colour, author and note.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAndReadsEachKind()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var points = Stroke;
        int[] strokes = [points.Length];

        var highlight = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sage, "Check this");
        _ = editor.AddMarkup(0, AnnotationKind.Underline, [Line], AnnotationColors.Slate, string.Empty);
        _ = editor.AddMarkup(0, AnnotationKind.StrikeOut, [Line], AnnotationColors.Clay, string.Empty);
        _ = editor.AddMarkup(0, AnnotationKind.Squiggly, [Line], AnnotationColors.Sand, string.Empty);
        _ = editor.AddInk(0, points, strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink);
        _ = editor.AddNote(0, NoteAt, "A sticky note", AnnotationColors.Sand);
        _ = editor.AddText(0, TextAt, "Written on the page\nSecond line", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        _ = editor.AddText(0, SignatureAt, "Glenn Watson", SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature);
        _ = editor.AddInk(0, points, strokes, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);

        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(0, annotations);

        AnnotationKind[] expected =
        [
            AnnotationKind.Highlight, AnnotationKind.Underline, AnnotationKind.StrikeOut, AnnotationKind.Squiggly, AnnotationKind.Ink,
            AnnotationKind.Note, AnnotationKind.TextBox, AnnotationKind.Signature, AnnotationKind.Signature,
        ];
        await Assert.That(editor.HasUnsavedChanges).IsTrue();
        await Assert.That(highlight).IsGreaterThanOrEqualTo(0);
        await Assert.That(annotations.Select(static a => a.Kind).ToArray()).IsEquivalentTo(expected);
        var first = annotations.First(a => a.Index == highlight);
        await Assert.That(first.Color).IsEqualTo(AnnotationColors.Sage);
        await Assert.That(first.Contents).IsEqualTo("Check this");
        await Assert.That(first.Author).IsEqualTo(Environment.UserName);
        await Assert.That(first.Bounds.Left).IsEqualTo(Line.Left).Within(Tolerance);
        await Assert.That(first.Bounds.Top).IsEqualTo(Line.Top).Within(Tolerance);
        await Assert.That(annotations.Single(static a => a.Kind == AnnotationKind.TextBox).Bounds.Width).IsGreaterThan(0);
    }

    /// <summary>Verifies an annotation can be recoloured, given a note and removed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesAndRemoves()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var index = editor.AddMarkup(0, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, string.Empty);
        var text = editor.AddText(0, TextAt, "Text", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);

        var recoloured = editor.SetColor(0, index, AnnotationColors.Clay);
        var recolouredText = editor.SetColor(0, text, AnnotationColors.Slate);
        var noted = editor.SetContents(0, index, "Changed");
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(0, annotations);
        var changed = annotations.Single(a => a.Index == index);
        var removed = editor.Remove(0, index);
        var after = new List<PageAnnotation>();
        editor.GetAnnotations(0, after);

        await Assert.That(recoloured).IsTrue();
        await Assert.That(recolouredText).IsTrue();
        await Assert.That(noted).IsTrue();
        await Assert.That(changed.Color).IsEqualTo(AnnotationColors.Clay);
        await Assert.That(changed.Contents).IsEqualTo("Changed");
        await Assert.That(removed).IsTrue();
        await Assert.That(after.Count).IsEqualTo(1);
    }

    /// <summary>Verifies annotations survive saving and reopening, and that they are drawn on the page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesAndReopens()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        _ = editor.AddMarkup(1, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, "Saved note");
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-annotations-{Guid.NewGuid():N}.pdf");
        try
        {
            bool saved;
            await using (var stream = File.Create(path))
            {
                saved = editor.Save(stream);
            }

            using var reopened = new PdfiumEngine().Open(path, null);
            var annotations = new List<PageAnnotation>();
            ((IAnnotationEditor)DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!).GetAnnotations(1, annotations);

            await Assert.That(saved).IsTrue();
            await Assert.That(editor.HasUnsavedChanges).IsFalse();
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(annotations[0].Contents).IsEqualTo("Saved note");
            await Assert.That(IsTinted(reopened)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Renders the highlighted line and checks it is tinted rather than white.</summary>
    /// <param name="document">The document.</param>
    /// <returns><see langword="true"/> when a pixel inside the highlight is not white.</returns>
    private static bool IsTinted(IDocument document)
    {
        const int bytesPerPixel = 4;
        const byte white = 0xFF;
        const float half = 0.5F;
        const float inset = 2;
        TestPdf.GetPageSize(1, out var width, out var height);
        var pixels = new byte[width * height * bytesPerPixel];
        _ = document.Render(new(1, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * bytesPerPixel));
        var offset = (((int)(Line.Top + inset) * width) + (int)(Line.Left + (Line.Width * half))) * bytesPerPixel;
        return pixels.AsSpan(offset, bytesPerPixel - 1).ContainsAnyExcept(white);
    }
}
