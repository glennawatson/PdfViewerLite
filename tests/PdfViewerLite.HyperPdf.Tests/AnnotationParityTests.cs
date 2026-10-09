// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>
/// Makes the same edits through the PDFium engine and the native editor, saves both, reopens both saved files with
/// PDFium and checks PDFium reads the same annotations and replies from each.
/// </summary>
public sealed class AnnotationParityTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 2;

    /// <summary>The annotations PDFium lists after the edits: every one added but the removed note and the replies.</summary>
    private const int Listed = 16;

    /// <summary>The replies made.</summary>
    private const int Replies = 2;

    /// <summary>The page edited.</summary>
    private const int Page = 1;

    /// <summary>How close bounds must be, in points.</summary>
    private const float BoundsTolerance = 1;

    /// <summary>How far colour channels may differ, from rounding when written.</summary>
    private const int ChannelTolerance = 1;

    /// <summary>The line width in points.</summary>
    private const float Width = 2;

    /// <summary>The thick line width in points.</summary>
    private const float ThickWidth = 5;

    /// <summary>The text size in points.</summary>
    private const float FontSize = 12;

    /// <summary>The larger text size in points.</summary>
    private const float LargeFontSize = 18;

    /// <summary>How far an annotation is moved, in points.</summary>
    private const float Shift = 40;

    /// <summary>The picture stamp's side in pixels.</summary>
    private const int PictureSide = 4;

    /// <summary>Bytes per pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>A grey pixel channel.</summary>
    private const byte Grey = 0x60;

    /// <summary>The bits of one colour channel.</summary>
    private const int ChannelBits = 8;

    /// <summary>The colour channels.</summary>
    private const int ChannelCount = 3;

    /// <summary>One channel's mask.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>A marked line.</summary>
    private static readonly PageRect Line = new(72, 100, 200, 14);

    /// <summary>A drawing.</summary>
    private static readonly PagePoint[] Stroke = [new(80, 300), new(120, 280), new(160, 320)];

    /// <summary>The drawing's length.</summary>
    private static readonly int[] Strokes = [Stroke.Length];

    /// <summary>A triangle.</summary>
    private static readonly PagePoint[] Triangle = [new(300, 100), new(400, 100), new(350, 180)];

    /// <summary>A square.</summary>
    private static readonly PagePoint[] Square = [new(100, 400), new(200, 400), new(200, 480), new(100, 480)];

    /// <summary>Where the note goes.</summary>
    private static readonly PagePoint NoteAt = new(450, 100);

    /// <summary>Where the note that is removed goes.</summary>
    private static readonly PagePoint RemovedAt = new(500, 500);

    /// <summary>The rectangle's top-left corner.</summary>
    private static readonly PagePoint BoxStart = new(72, 200);

    /// <summary>The rectangle's bottom-right corner.</summary>
    private static readonly PagePoint BoxEnd = new(172, 260);

    /// <summary>Where the rectangle is moved to.</summary>
    private static readonly PageRect BoxMoved = new(80, 210, 120, 60);

    /// <summary>The ellipse's top-left corner.</summary>
    private static readonly PagePoint OvalStart = new(300, 200);

    /// <summary>The ellipse's bottom-right corner.</summary>
    private static readonly PagePoint OvalEnd = new(400, 260);

    /// <summary>The arrow's tail.</summary>
    private static readonly PagePoint ArrowTail = new(72, 400);

    /// <summary>The arrow's point.</summary>
    private static readonly PagePoint ArrowTip = new(200, 400);

    /// <summary>The line's start.</summary>
    private static readonly PagePoint LineStart = new(72, 420);

    /// <summary>The line's end.</summary>
    private static readonly PagePoint LineEnd = new(200, 440);

    /// <summary>Where the text goes.</summary>
    private static readonly PagePoint TextAt = new(300, 500);

    /// <summary>Where the typed signature goes.</summary>
    private static readonly PagePoint SignatureAt = new(300, 560);

    /// <summary>Where the stamp goes.</summary>
    private static readonly PagePoint StampAt = new(300, 620);

    /// <summary>What the callout points at.</summary>
    private static readonly PagePoint CalloutTarget = new(72, 650);

    /// <summary>Where the callout's text goes.</summary>
    private static readonly PagePoint CalloutText = new(150, 680);

    /// <summary>Where a picture goes.</summary>
    private static readonly PageRect PictureAt = new(400, 600, 60, 40);

    /// <summary>PDFium reads the same annotations and replies from the file each engine saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BothEnginesSaveWhatPdfiumReadsAlike()
    {
        var expected = SaveAndRead(EditWithPdfium());
        var actual = SaveAndRead(EditNatively());

        await Assert.That(expected.Annotations.Count).IsEqualTo(Listed);
        await Assert.That(expected.Replies.Length).IsEqualTo(Replies);
        await Assert.That(actual.Annotations.Count).IsEqualTo(expected.Annotations.Count);
        for (var i = 0; i < expected.Annotations.Count; i++)
        {
            var want = expected.Annotations[i];
            var got = actual.Annotations[i];
            await Assert.That(got.Kind).IsEqualTo(want.Kind);
            await Assert.That(got.Index).IsEqualTo(want.Index);
            await Assert.That(got.Bounds.Left).IsEqualTo(want.Bounds.Left).Within(BoundsTolerance);
            await Assert.That(got.Bounds.Top).IsEqualTo(want.Bounds.Top).Within(BoundsTolerance);
            await Assert.That(got.Bounds.Right).IsEqualTo(want.Bounds.Right).Within(BoundsTolerance);
            await Assert.That(got.Bounds.Bottom).IsEqualTo(want.Bounds.Bottom).Within(BoundsTolerance);
            await Assert.That(ColorDistance(got.Color, want.Color)).IsLessThanOrEqualTo(ChannelTolerance);
            await Assert.That(got.Contents).IsEqualTo(want.Contents);
            await Assert.That(got.Author).IsEqualTo(want.Author);
            await Assert.That(got.LineWidth).IsEqualTo(want.LineWidth);
            await Assert.That(got.FontSize).IsEqualTo(want.FontSize);
        }

        await Assert.That(actual.Replies).IsEquivalentTo(expected.Replies);
    }

    /// <summary>Opens the test document with PDFium and edits it.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] EditWithPdfium()
    {
        using var document = NativeDocument.OpenWithPdfium(TestPdf.Create(PageCount), out var path);
        try
        {
            var editor = (IAnnotationEditor)document;
            Edit(editor);
            return NativeDocument.Save(editor);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Opens the test document with HyperPDF and edits it natively.</summary>
    /// <returns>The saved file.</returns>
    private static byte[] EditNatively()
    {
        using var test = new NativeDocument(PageCount);
        Edit(test.Editor);
        return NativeDocument.Save(test.Editor);
    }

    /// <summary>Makes the same edits through either engine.</summary>
    /// <param name="editor">The editor.</param>
    private static void Edit(IAnnotationEditor editor)
    {
        editor.Author = "Parity Tester";
        var highlight = editor.AddMarkup(Page, AnnotationKind.Highlight, [Line], AnnotationColors.Sand, "Check");
        _ = editor.AddMarkup(Page, AnnotationKind.StrikeOut, [Line with { Top = Line.Top + Shift }], AnnotationColors.Clay, string.Empty);
        var ink = editor.AddInk(Page, Stroke, Strokes, AnnotationColors.Ink, Width, AnnotationKind.Ink);
        var note = editor.AddNote(Page, NoteAt, "Note", AnnotationColors.Sage);
        var box = editor.AddShape(Page, AnnotationKind.Rectangle, BoxStart, BoxEnd, AnnotationColors.Clay, Width);
        _ = editor.AddShape(Page, AnnotationKind.Ellipse, OvalStart, OvalEnd, AnnotationColors.Slate, Width);
        _ = editor.AddShape(Page, AnnotationKind.Arrow, ArrowTail, ArrowTip, AnnotationColors.Ink, Width);
        _ = editor.AddShape(Page, AnnotationKind.Line, LineStart, LineEnd, AnnotationColors.Sage, Width);
        _ = editor.AddPolygon(Page, AnnotationKind.Polygon, Triangle, AnnotationColors.Slate, Width);
        var cloud = editor.AddPolygon(Page, AnnotationKind.Cloud, Square, AnnotationColors.Clay, Width);
        _ = editor.AddPolygon(Page, AnnotationKind.PolyLine, Stroke, AnnotationColors.Ink, Width);
        var removed = editor.AddNote(Page, RemovedAt, "Removed", AnnotationColors.Sand);
        var pixels = new byte[PictureSide * PictureSide * BytesPerPixel];
        Array.Fill(pixels, Grey);
        _ = editor.AddImageStamp(Page, PictureAt, pixels, PictureSide, PictureSide);
        var text = editor.AddText(Page, TextAt, "Typed words\nand more", FontSize, AnnotationColors.Ink, AnnotationKind.TextBox);
        _ = editor.AddText(Page, SignatureAt, "Glenn Watson", LargeFontSize, AnnotationColors.Ink, AnnotationKind.Signature);
        _ = editor.AddStamp(Page, StampAt, "APPROVED", AnnotationColors.Clay);
        _ = editor.AddCallout(Page, CalloutTarget, CalloutText, "Look here", FontSize, AnnotationColors.Ink);
        _ = editor.AddReply(Page, note, "Agreed", ReviewState.None);
        _ = editor.AddReply(Page, note, string.Empty, ReviewState.Rejected);
        _ = editor.SetColor(Page, highlight, AnnotationColors.Heather);
        _ = editor.SetContents(Page, note, "Changed note");
        _ = editor.SetLineWidth(Page, ink, ThickWidth);
        _ = editor.SetLineWidth(Page, box, ThickWidth);
        _ = editor.SetBounds(Page, box, BoxMoved);
        _ = editor.SetColor(Page, cloud, AnnotationColors.Peach);
        _ = editor.SetFontSize(Page, text, LargeFontSize);
        _ = editor.SetRemoved(Page, removed, true);
    }

    /// <summary>Saves nothing more; reopens a saved file with PDFium and reads the page's annotations and every comment's replies.</summary>
    /// <param name="saved">The saved file.</param>
    /// <returns>What PDFium reads.</returns>
    private static ReadBack SaveAndRead(byte[] saved)
    {
        using var document = NativeDocument.OpenWithPdfium(saved, out var path);
        try
        {
            var editor = (IAnnotationEditor)document;
            var annotations = NativeDocument.Read(editor, Page);
            var replies = new List<AnnotationReply>();
            foreach (var annotation in annotations)
            {
                editor.GetReplies(Page, annotation.Index, replies);
            }

            return new(annotations, [.. replies.Select(static r => r with { Index = 0 })]);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Gets the largest difference between two colours' channels.</summary>
    /// <param name="a">One colour.</param>
    /// <param name="b">The other.</param>
    /// <returns>The difference.</returns>
    private static int ColorDistance(uint a, uint b)
    {
        var largest = 0;
        for (var i = 0; i < ChannelCount; i++)
        {
            var shift = i * ChannelBits;
            largest = Math.Max(largest, Math.Abs((int)((a >> shift) & ChannelMask) - (int)((b >> shift) & ChannelMask)));
        }

        return largest;
    }

    /// <summary>What PDFium read from a saved file.</summary>
    /// <param name="Annotations">The page's annotations.</param>
    /// <param name="Replies">Every comment's replies, without their indexes.</param>
    private sealed record ReadBack(List<PageAnnotation> Annotations, AnnotationReply[] Replies);
}
