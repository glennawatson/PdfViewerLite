// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Annotations;

/// <summary>
/// Reads, adds and changes annotations, and saves the document. Implemented by documents that support editing;
/// safe to call from any thread. Positions are in page space (points, top-left origin).
/// </summary>
public interface IAnnotationEditor
{
    /// <summary>Gets a value indicating whether the document has changes that are not saved.</summary>
    bool HasUnsavedChanges { get; }

    /// <summary>Appends the annotations on a page, skipping links, form fields and pop-ups.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the annotations.</param>
    void GetAnnotations(int pageIndex, List<PageAnnotation> output);

    /// <summary>Marks text with a highlight, underline, strike-out or squiggly line.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind">One of the text markup kinds.</param>
    /// <param name="lines">The line rectangles to mark, for example from a text selection.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="contents">An optional note, or an empty string.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents);

    /// <summary>Adds a freehand drawing, or a drawn signature.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="points">Every point, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <param name="kind"><see cref="AnnotationKind.Ink"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind);

    /// <summary>Adds a sticky note.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the note icon.</param>
    /// <param name="contents">The note text.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddNote(int pageIndex, PagePoint location, string contents, uint color);

    /// <summary>Writes text on the page, or places a typed signature.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the first line.</param>
    /// <param name="text">The text; line breaks start new lines.</param>
    /// <param name="fontSize">The font size in points.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="kind"><see cref="AnnotationKind.TextBox"/> or <see cref="AnnotationKind.Signature"/>.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddText(int pageIndex, PagePoint location, string text, float fontSize, uint color, AnnotationKind kind);

    /// <summary>Draws a rectangle, ellipse, arrow or line between two points.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="kind"><see cref="AnnotationKind.Rectangle"/>, <see cref="AnnotationKind.Ellipse"/>, <see cref="AnnotationKind.Arrow"/> or <see cref="AnnotationKind.Line"/>.</param>
    /// <param name="start">Where the drag started: a corner, or the arrow's tail.</param>
    /// <param name="end">Where the drag ended: the opposite corner, or the arrow's point.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <param name="width">The line width in points.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width);

    /// <summary>Places a stamp: a framed word such as "APPROVED".</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="location">The top-left corner of the stamp.</param>
    /// <param name="label">The word on the stamp.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    int AddStamp(int pageIndex, PagePoint location, string label, uint color);

    /// <summary>Appends the replies to a comment and its review status changes, oldest first.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="output">The list receiving the replies.</param>
    void GetReplies(int pageIndex, int index, List<AnnotationReply> output);

    /// <summary>Replies to a comment, or records a review status for it.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The comment's annotation index.</param>
    /// <param name="contents">The reply's text; may be empty when setting a status.</param>
    /// <param name="state">The review status to record, or <see cref="ReviewState.None"/> for a plain reply.</param>
    /// <returns>The reply's annotation index, or -1.</returns>
    int AddReply(int pageIndex, int index, string contents, ReviewState state);

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetColor(int pageIndex, int index, uint color);

    /// <summary>Changes an annotation's note text.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="contents">The note text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetContents(int pageIndex, int index, string contents);

    /// <summary>Removes an annotation.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    bool Remove(int pageIndex, int index);

    /// <summary>
    /// Saves the document to a stream. Documents with digital signatures are saved incrementally so the signatures stay
    /// valid; others are saved compactly.
    /// </summary>
    /// <param name="destination">The stream to write to.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    bool Save(Stream destination);
}
