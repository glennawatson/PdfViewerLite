// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Shapes and stamps: rectangles and ellipses as PDF Square and Circle annotations (PDFium draws their appearance),
/// arrows and lines as ink annotations marked by their subject so every reader shows them, and stamps as a framed
/// word drawn from page objects.
/// </summary>
internal static partial class PdfiumAnnotations
{
    /// <summary>PDFium's Square annotation subtype.</summary>
    private const int SubtypeSquare = 5;

    /// <summary>PDFium's Circle annotation subtype.</summary>
    private const int SubtypeCircle = 6;

    /// <summary>The subject marking an arrow drawn as ink.</summary>
    private const string ArrowSubject = "Arrow";

    /// <summary>The subject marking a line drawn as ink.</summary>
    private const string LineSubject = "Line";

    /// <summary>The subject marking a stamp this viewer placed.</summary>
    private const string StampSubject = "Stamp";

    /// <summary>The length of an arrow's head as a multiple of the line width, never shorter than <see cref="MinArrowHead"/>.</summary>
    private const float ArrowHeadWidths = 4;

    /// <summary>The shortest arrow head in points.</summary>
    private const float MinArrowHead = 8;

    /// <summary>The angle between the shaft and each side of the arrow head, in radians (about 25 degrees).</summary>
    private const double ArrowHeadAngle = 0.436;

    /// <summary>The font size of a stamp's word.</summary>
    private const float StampFontSize = 18;

    /// <summary>The space between a stamp's word and its frame.</summary>
    private const float StampPadding = 4;

    /// <summary>The width of a stamp's frame.</summary>
    private const float StampFrame = 2;

    /// <summary>The points of an arrow: tail to point, then the two sides of the head.</summary>
    private const int ArrowPoints = 6;

    /// <summary>The strokes of an arrow: the shaft and the two sides of its head.</summary>
    private const int ArrowStrokes = 3;

    /// <summary>The points in one straight stroke.</summary>
    private const int StrokePoints = 2;

    /// <summary>Halves a line width for the margin around a shape.</summary>
    private const float Half = 0.5F;

    /// <summary>Adds a rectangle, ellipse, arrow or line.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="start">Where the drag started.</param>
    /// <param name="end">Where the drag ended.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddShape(PdfiumPage page, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width) => kind switch
    {
        AnnotationKind.Rectangle => AddBox(page, SubtypeSquare, start, end, color, width),
        AnnotationKind.Ellipse => AddBox(page, SubtypeCircle, start, end, color, width),
        AnnotationKind.Arrow => AddArrow(page, start, end, color, width),
        AnnotationKind.Line => AddInk(page, [start, end], [StrokePoints], color, width, LineSubject),
        _ => -1,
    };

    /// <summary>Places a stamp: a word in a frame.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The font.</param>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner.</param>
    /// <param name="label">The word.</param>
    /// <param name="color">The colour.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddStamp(PdfiumDocumentHandle document, PdfiumFontHandle font, PdfiumPage page, PagePoint location, string label, uint color)
    {
        if (string.IsNullOrWhiteSpace(label))
        {
            return -1;
        }

        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeStamp);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            page.ToPdf(new(location.X + StampPadding + StampFrame, location.Y + StampPadding + StampFrame), out var left, out var top);
            PdfBounds textBounds = default;
            var text = CreateTextObject(document, new(font, StampFontSize, color), label, left, top - (StampFontSize * Ascent), ref textBounds);
            var box = textBounds.ToRect(StampPadding);
            var frame = NativeMethods.FPDFPageObj_CreateNewRect(box.Left, box.Bottom, box.Right - box.Left, box.Top - box.Bottom);
            _ = NativeMethods.FPDFPath_SetDrawMode(frame, 0, 1);
            _ = NativeMethods.FPDFPageObj_SetStrokeWidth(frame, StampFrame);
            _ = NativeMethods.FPDFPageObj_SetStrokeColor(frame, (color >> RedShift) & ChannelMask, (color >> GreenShift) & ChannelMask, color & ChannelMask, Opaque);

            // The stamp's appearance box comes from its rectangle, so set it before appending the objects.
            Finish(annotation, textBounds.ToRect(StampPadding + StampFrame), color, label, StampSubject);
            _ = NativeMethods.FPDFAnnot_AppendObject(annotation, frame);
            _ = NativeMethods.FPDFAnnot_AppendObject(annotation, text);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Adds a rectangle or ellipse filling the box between two corners.</summary>
    /// <param name="page">The page.</param>
    /// <param name="subtype">Square or Circle.</param>
    /// <param name="start">One corner.</param>
    /// <param name="end">The opposite corner.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <returns>The annotation index, or -1.</returns>
    private static int AddBox(PdfiumPage page, int subtype, PagePoint start, PagePoint end, uint color, float width)
    {
        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, subtype);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            PdfBounds bounds = default;
            bounds.Add(page, start);
            bounds.Add(page, end);
            _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
            Finish(annotation, bounds.ToRect(width * Half), color, string.Empty, null);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Adds an arrow: a shaft from tail to point and two short strokes forming the head.</summary>
    /// <param name="page">The page.</param>
    /// <param name="tail">The tail.</param>
    /// <param name="point">The point.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <returns>The annotation index, or -1.</returns>
    private static int AddArrow(PdfiumPage page, PagePoint tail, PagePoint point, uint color, float width)
    {
        var dx = tail.X - point.X;
        var dy = tail.Y - point.Y;
        var length = Math.Sqrt((dx * dx) + (dy * dy));
        if (length <= 0)
        {
            return -1;
        }

        var head = Math.Max(MinArrowHead, ArrowHeadWidths * width);
        var angle = Math.Atan2(dy, dx);
        Span<PagePoint> points = stackalloc PagePoint[ArrowPoints];
        points[0] = tail;
        points[1] = point;
        points[2] = point;
        points[3] = Toward(point, angle + ArrowHeadAngle, head);
        points[4] = point;
        points[5] = Toward(point, angle - ArrowHeadAngle, head);
        Span<int> strokes = stackalloc int[ArrowStrokes];
        strokes.Fill(StrokePoints);
        return AddInk(page, points, strokes, color, width, ArrowSubject);
    }

    /// <summary>Gets the point a distance away in a direction.</summary>
    /// <param name="origin">The start.</param>
    /// <param name="angle">The direction in radians.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The point.</returns>
    private static PagePoint Toward(PagePoint origin, double angle, double distance) =>
        new((float)(origin.X + (Math.Cos(angle) * distance)), (float)(origin.Y + (Math.Sin(angle) * distance)));

    /// <summary>Adds straight strokes as an ink annotation marked with a subject.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="strokeLengths">The points in each stroke.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <param name="subject">What the strokes are.</param>
    /// <returns>The annotation index, or -1.</returns>
    private static int AddInk(PdfiumPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, string subject)
    {
        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeInk);
        if (annotation == 0)
        {
            return -1;
        }

        try
        {
            PdfBounds bounds = default;
            var offset = 0;
            foreach (var length in strokeLengths)
            {
                AddStroke(page, annotation, points.Slice(offset, length), ref bounds);
                offset += length;
            }

            _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
            Finish(annotation, bounds.ToRect(width), color, string.Empty, subject);
            return NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }
}
