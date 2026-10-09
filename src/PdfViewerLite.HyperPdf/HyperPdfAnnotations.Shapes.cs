// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.HyperPdf;

/// <content>
/// Text markup, drawings, notes, shapes and polygons. Text markup, notes, rectangles and ellipses get no appearance,
/// as the PDFium engine writes them, so readers draw them from their entries; strokes get an appearance drawn here.
/// </content>
internal sealed partial class HyperPdfAnnotations
{
    /// <summary>The size of a sticky note icon in points.</summary>
    private const float NoteSize = 20;

    /// <summary>Halves a line width for the margin around a rectangle or ellipse.</summary>
    private const float Half = 0.5F;

    /// <summary>The corners of one marked line.</summary>
    private const int QuadCorners = 4;

    /// <summary>The points converted on the stack before a pooled buffer is used.</summary>
    private const int StackPoints = 128;

    /// <summary>The points in one straight stroke.</summary>
    private const int StrokePoints = 2;

    /// <summary>The fewest points of a closed shape.</summary>
    private const int MinPolygonPoints = 3;

    /// <summary>The fewest points of a run of lines.</summary>
    private const int MinPolyLinePoints = 2;

    /// <summary>The index of the bottom-left corner of a quadrilateral.</summary>
    private const int QuadBottomLeft = 2;

    /// <summary>The index of the bottom-right corner of a quadrilateral.</summary>
    private const int QuadBottomRight = 3;

    /// <inheritdoc/>
    public int AddMarkup(int pageIndex, AnnotationKind kind, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        ArgumentNullException.ThrowIfNull(contents);
        if (kind == AnnotationKind.Redaction)
        {
            return AddRedaction(pageIndex, lines);
        }

        var subtype = kind switch
        {
            AnnotationKind.Highlight => KnownName.Highlight,
            AnnotationKind.Underline => KnownName.Underline,
            AnnotationKind.StrikeOut => KnownName.StrikeOut,
            AnnotationKind.Squiggly => KnownName.Squiggly,
            _ => KnownName.None,
        };
        if (subtype == KnownName.None || lines.IsEmpty)
        {
            return -1;
        }

        lock (_gate)
        {
            return GetPage(pageIndex) is { } page ? AddMarkup(pageIndex, page, subtype, lines, color, contents) : -1;
        }
    }

    /// <inheritdoc/>
    public int AddInk(int pageIndex, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, uint color, float width, AnnotationKind kind)
    {
        if (points.IsEmpty || strokeLengths.IsEmpty)
        {
            return -1;
        }

        lock (_gate)
        {
            var inkKind = kind == AnnotationKind.Signature ? AnnotationKind.Signature : AnnotationKind.Ink;
            return GetPage(pageIndex) is { } page ? AddStrokes(pageIndex, page, points, strokeLengths, new(KnownName.Ink, inkKind, color, width)) : -1;
        }
    }

    /// <inheritdoc/>
    public int AddNote(int pageIndex, PagePoint location, string contents, uint color)
    {
        ArgumentNullException.ThrowIfNull(contents);
        lock (_gate)
        {
            if (GetPage(pageIndex) is not { } page)
            {
                return -1;
            }

            var bounds = default(UserBounds);
            bounds.Add(ToUser(page, location));
            bounds.Add(ToUser(page, new(location.X + NoteSize, location.Y + NoteSize)));
            var note = PdfAnnotations.Create(_store, KnownName.Text, bounds.ToRectangle(0));
            return Add(pageIndex, page, note, color, contents, []);
        }
    }

    /// <inheritdoc/>
    public int AddShape(int pageIndex, AnnotationKind kind, PagePoint start, PagePoint end, uint color, float width)
    {
        if (kind == AnnotationKind.Redaction)
        {
            return AddRedaction(pageIndex, start, end);
        }

        lock (_gate)
        {
            return GetPage(pageIndex) is not { } page ? -1 : kind switch
            {
                AnnotationKind.Rectangle => AddBox(pageIndex, page, start, end, new(KnownName.Square, kind, color, width)),
                AnnotationKind.Ellipse => AddBox(pageIndex, page, start, end, new(KnownName.Circle, kind, color, width)),
                AnnotationKind.Arrow => AddArrow(pageIndex, page, start, end, new(KnownName.Ink, kind, color, width)),
                AnnotationKind.Line => AddStrokes(pageIndex, page, [start, end], [StrokePoints], new(KnownName.Ink, kind, color, width)),
                _ => -1,
            };
        }
    }

    /// <inheritdoc/>
    public int AddPolygon(int pageIndex, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width)
    {
        var subtype = kind switch
        {
            AnnotationKind.Polygon or AnnotationKind.Cloud => KnownName.Polygon,
            AnnotationKind.PolyLine => KnownName.PolyLine,
            _ => KnownName.None,
        };
        var fewest = subtype == KnownName.PolyLine ? MinPolyLinePoints : MinPolygonPoints;
        if (subtype == KnownName.None || vertices.Length < fewest || width <= 0)
        {
            return -1;
        }

        lock (_gate)
        {
            return GetPage(pageIndex) is { } page ? AddStrokes(pageIndex, page, vertices, [vertices.Length], new(subtype, kind, color, width)) : -1;
        }
    }

    /// <summary>Gets the subject that marks a kind drawn as strokes.</summary>
    /// <param name="kind">The kind.</param>
    /// <returns>The subject, or empty for freehand ink.</returns>
    private static ReadOnlySpan<byte> StrokeSubject(AnnotationKind kind) => kind switch
    {
        AnnotationKind.Signature => SignatureSubject,
        AnnotationKind.Arrow => ArrowSubject,
        AnnotationKind.Line => LineSubject,
        AnnotationKind.Polygon => PolygonSubject,
        AnnotationKind.Cloud => CloudSubject,
        AnnotationKind.PolyLine => PolyLineSubject,
        _ => [],
    };

    /// <summary>Converts strokes to user space, stopping at the first stroke that runs past the points.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points in page space.</param>
    /// <param name="lengths">The number of points in each stroke.</param>
    /// <param name="path">Receives the strokes.</param>
    private static void ConvertStrokes(PdfPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> lengths, ref PdfStrokeBuffer path)
    {
        var offset = 0;
        foreach (var length in lengths)
        {
            if (length <= 0 || offset + length > points.Length)
            {
                return;
            }

            foreach (var point in points.Slice(offset, length))
            {
                path.Add(ToUser(page, point));
            }

            path.EndStroke();
            offset += length;
        }
    }

    /// <summary>Adds text markup over the given lines.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="subtype">The markup subtype.</param>
    /// <param name="lines">The marked lines.</param>
    /// <param name="color">The colour.</param>
    /// <param name="contents">The note text.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddMarkup(int pageIndex, PdfPage page, KnownName subtype, ReadOnlySpan<PageRect> lines, uint color, string contents)
    {
        var count = lines.Length * QuadCorners;
        Vector2[]? rented = null;
        var corners = count <= StackPoints ? stackalloc Vector2[StackPoints] : (rented = ArrayPool<Vector2>.Shared.Rent(count));
        try
        {
            var bounds = default(UserBounds);
            for (var i = 0; i < lines.Length; i++)
            {
                var line = lines[i];
                var quad = corners.Slice(i * QuadCorners, QuadCorners);
                quad[0] = ToUser(page, new(line.Left, line.Top));
                quad[1] = ToUser(page, new(line.Right, line.Top));
                quad[QuadBottomLeft] = ToUser(page, new(line.Left, line.Bottom));
                quad[QuadBottomRight] = ToUser(page, new(line.Right, line.Bottom));
                foreach (var corner in quad)
                {
                    bounds.Add(corner);
                }
            }

            var markup = PdfAnnotations.Create(_store, subtype, bounds.ToRectangle(0));
            PdfAnnotations.SetQuadPoints(markup, corners[..count]);
            return Add(pageIndex, page, markup, color, contents, []);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<Vector2>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Adds a rectangle or ellipse filling the box between two corners.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="start">One corner.</param>
    /// <param name="end">The opposite corner.</param>
    /// <param name="style">Square or Circle, the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddBox(int pageIndex, PdfPage page, PagePoint start, PagePoint end, StrokeStyle style)
    {
        var bounds = default(UserBounds);
        bounds.Add(ToUser(page, start));
        bounds.Add(ToUser(page, end));
        var box = PdfAnnotations.Create(_store, style.Subtype, bounds.ToRectangle(style.Width * Half));
        PdfAnnotations.SetBorderWidth(box, style.Width);
        return Add(pageIndex, page, box, style.Color, string.Empty, []);
    }

    /// <summary>Adds an arrow: a shaft from tail to point and two short strokes forming the head.</summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="tailPoint">The tail.</param>
    /// <param name="tipPoint">The point.</param>
    /// <param name="style">The ink subtype, the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddArrow(int pageIndex, PdfPage page, PagePoint tailPoint, PagePoint tipPoint, StrokeStyle style)
    {
        var tail = new Vector2(tailPoint.X, tailPoint.Y);
        var tip = new Vector2(tipPoint.X, tipPoint.Y);
        if ((tail - tip).LengthSquared() <= 0)
        {
            return -1;
        }

        var (first, second) = PdfAppearances.OpenArrowHead(tip, tail, ArrowHead(style.Width));
        Span<PagePoint> points = [tailPoint, tipPoint, tipPoint, new(first.X, first.Y), tipPoint, new(second.X, second.Y)];
        Span<int> strokes = [StrokePoints, StrokePoints, StrokePoints];
        return AddStrokes(pageIndex, page, points, strokes, style);
    }

    /// <summary>
    /// Adds strokes: ink, or a polygon's vertices. The points become the annotation's <c>/InkList</c> or
    /// <c>/Vertices</c> and are drawn into its appearance.
    /// </summary>
    /// <param name="pageIndex">The page index.</param>
    /// <param name="page">The page.</param>
    /// <param name="points">The points in page space, stroke after stroke.</param>
    /// <param name="lengths">The number of points in each stroke.</param>
    /// <param name="style">The subtype, the kind (which picks the subject and how points join), the colour and the line width.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private int AddStrokes(int pageIndex, PdfPage page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> lengths, StrokeStyle style)
    {
        var path = default(PdfStrokeBuffer);
        try
        {
            ConvertStrokes(page, points, lengths, ref path);
            if (path.PointCount == 0)
            {
                return -1;
            }

            var annotation = PdfAnnotations.Create(_store, style.Subtype, default);
            PdfAnnotations.SetBorderWidth(annotation, style.Width);
            WriteStrokes(annotation, ref path);
            WritePolygonEntries(annotation, style.Kind);
            Redraw(annotation, style.Kind, ref path, style.Color, style.Width);
            return Add(pageIndex, page, annotation, style.Color, string.Empty, StrokeSubject(style.Kind));
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Gives a cloud its intent and cloudy border effect.</summary>
    /// <param name="annotation">The polygon.</param>
    /// <param name="kind">Its kind.</param>
    private void WritePolygonEntries(PdfDictionary annotation, AnnotationKind kind)
    {
        if (kind != AnnotationKind.Cloud)
        {
            return;
        }

        annotation.Set(KnownName.IT, PdfValue.FromName(_names.PolygonCloud));
        var effect = new PdfDictionary(_store, StrokePoints);
        effect.Set(KnownName.S, PdfValue.FromName(KnownName.C));
        effect.Set(KnownName.I, PdfValue.FromInteger(1));
        annotation.Set(KnownName.BE, PdfValue.FromDictionary(effect));
    }
}
