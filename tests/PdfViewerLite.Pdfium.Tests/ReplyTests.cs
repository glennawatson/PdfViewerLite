// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for comment replies and review status: they thread under their comment, stay off the page list, and are saved as standard <c>/IRT</c> replies.</summary>
public sealed class ReplyTests
{
    /// <summary>The number of pages in the generated document.</summary>
    private const int PageCount = 1;

    /// <summary>Two comments, or two replies.</summary>
    private const int Two = 2;

    /// <summary>Where the unrelated comment goes.</summary>
    private static readonly PagePoint OtherAt = new(300, 300);

    /// <summary>Where the comment goes.</summary>
    private static readonly PagePoint NoteAt = new(100, 100);

    /// <summary>Replies and a status are read back under their comment, oldest first, and not listed on their own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThreadsRepliesUnderTheirComment()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var note = editor.AddNote(0, NoteAt, "Is this figure right?", AnnotationColors.Sand);
        var other = editor.AddNote(0, OtherAt, "Unrelated", AnnotationColors.Sage);

        var first = editor.AddReply(0, note, "Yes, checked against the source.", ReviewState.None);
        var status = editor.AddReply(0, note, string.Empty, ReviewState.Accepted);
        var empty = editor.AddReply(0, note, " ", ReviewState.None);
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(0, annotations);
        var replies = new List<AnnotationReply>();
        editor.GetReplies(0, note, replies);
        var otherReplies = new List<AnnotationReply>();
        editor.GetReplies(0, other, otherReplies);

        await Assert.That(first).IsGreaterThanOrEqualTo(0);
        await Assert.That(status).IsGreaterThanOrEqualTo(0);
        await Assert.That(empty).IsEqualTo(-1);
        await Assert.That(annotations.Count).IsEqualTo(Two);
        await Assert.That(replies.Count).IsEqualTo(Two);
        await Assert.That(replies[0].Contents).IsEqualTo("Yes, checked against the source.");
        await Assert.That(replies[0].Author).IsEqualTo(Environment.UserName);
        await Assert.That(replies[1].State).IsEqualTo(ReviewState.Accepted);
        await Assert.That(otherReplies.Count).IsEqualTo(0);
    }

    /// <summary>Saving writes standard <c>/IRT</c> references, which PDFium reads back as the same thread.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesStandardReplies()
    {
        using var test = new TestDocument(PageCount);
        var editor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(test.Document, typeof(IAnnotationEditor))!;
        var note = editor.AddNote(0, NoteAt, "Please confirm.", AnnotationColors.Sand);
        _ = editor.AddReply(0, note, "Confirmed.", ReviewState.None);
        _ = editor.AddReply(0, note, string.Empty, ReviewState.Completed);
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-replies-{Guid.NewGuid():N}.pdf");
        try
        {
            await using (var stream = File.Create(path))
            {
                _ = editor.Save(stream);
            }

            var bytes = await File.ReadAllBytesAsync(path);
            var text = Encoding.Latin1.GetString(bytes);
            using var reopened = new PdfiumEngine().Open(path, null);
            var reopenedEditor = (IAnnotationEditor)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(reopened, typeof(IAnnotationEditor))!;
            var annotations = new List<PageAnnotation>();
            reopenedEditor.GetAnnotations(0, annotations);
            var replies = new List<AnnotationReply>();
            reopenedEditor.GetReplies(0, annotations[0].Index, replies);

            await Assert.That(text).Contains("/IRT");
            await Assert.That(text).Contains("/RT /R");
            await Assert.That(annotations.Count).IsEqualTo(1);
            await Assert.That(replies.Select(static r => r.Contents).ToArray()).IsEquivalentTo(["Confirmed.", string.Empty]);
            await Assert.That(replies[1].State).IsEqualTo(ReviewState.Completed);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
