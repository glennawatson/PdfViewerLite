// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Moving, resizing, restyling and removing annotations. Strokes (drawings, arrows, lines and polygons) are rewritten
/// point by point and drawn again into a new appearance stream, so lines keep their width; other annotations keep their
/// appearance, which PDF readers scale from its box into the annotation's rectangle. A removed annotation is hidden and
/// marked, keeping its index so the removal can be undone; it is left out when the file is saved.
/// </summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>The "hidden" annotation flag.</summary>
    private const int FlagHidden = 2;

    /// <summary>The characters a stored number can take.</summary>
    private const int NumberChars = 32;

    /// <summary>The line width used when an annotation does not record one.</summary>
    private const float DefaultLineWidth = 1;

    /// <summary>The characters expected in a drawn appearance.</summary>
    private const int AppearanceChars = 1024;

    /// <summary>The smallest box side, in points, a resize can make.</summary>
    private const float MinSide = 1;

    /// <summary>Gets the key marking a removed annotation; its value is the flags to restore.</summary>
    private static ReadOnlySpan<byte> RemovedKey => "PVLRemoved"u8;

    /// <summary>Gets the key of entries written into the annotation when the file is saved.</summary>
    private static ReadOnlySpan<byte> EntriesKey => "PVLEntries"u8;

    /// <summary>Hides an annotation and marks it removed, or brings it back with its old flags.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="removed">Whether to remove it.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetRemoved(PdfiumPage page, int index, bool removed)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            if (IsRemoved(annotation) == removed)
            {
                return false;
            }

            if (removed)
            {
                var flags = NativeMethods.FPDFAnnot_GetFlags(annotation);
                return SetNumber(annotation, RemovedKey, flags) && NativeMethods.FPDFAnnot_SetFlags(annotation, flags | FlagHidden) != 0;
            }

            var original = (int)ReadNumber(annotation, RemovedKey);
            return NativeMethods.FPDFAnnot_SetFlags(annotation, original) != 0 && SetString(annotation, RemovedKey, string.Empty);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Moves or resizes an annotation.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetBounds(PdfiumPage page, int index, PageRect bounds)
    {
        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            return GetKind(annotation) is { } kind && IsEditable(annotation, kind) && Move(page, annotation, kind, bounds) && SetModified(annotation);
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Changes the line width of a drawing, line, shape or polygon.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <param name="width">The width in points.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    internal static bool SetLineWidth(PdfiumPage page, int index, float width)
    {
        if (width <= 0)
        {
            return false;
        }

        var annotation = NativeMethods.FPDFPage_GetAnnot(page.Handle, index);
        if (annotation == 0)
        {
            return false;
        }

        try
        {
            if (GetKind(annotation) is not { } kind || !IsEditable(annotation, kind))
            {
                return false;
            }

            var subtype = NativeMethods.FPDFAnnot_GetSubtype(annotation);
            switch (subtype)
            {
                case SubtypeInk or SubtypePolygon or SubtypePolyline:
                {
                    _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
                    return MoveStrokes(annotation, subtype, kind, RectMap.Identity) && SetModified(annotation);
                }

                case SubtypeSquare or SubtypeCircle:
                {
                    _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
                    _ = NativeMethods.FPDFAnnot_SetAP(annotation, AppearanceNormal, null);
                    return SetModified(annotation);
                }

                default:
                {
                    return false;
                }
            }
        }
        finally
        {
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Moves an annotation the way its kind needs.</summary>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="bounds">The new bounds in page space.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private static bool Move(PdfiumPage page, nint annotation, AnnotationKind kind, PageRect bounds)
    {
        if (NativeMethods.FPDFAnnot_GetRect(annotation, out var old) == 0)
        {
            return false;
        }

        PdfBounds target = default;
        target.Add(page, new(bounds.Left, bounds.Top));
        target.Add(page, new(bounds.Right, bounds.Bottom));
        var map = new RectMap(old, target.ToRect(0));
        var subtype = NativeMethods.FPDFAnnot_GetSubtype(annotation);
        return subtype switch
        {
            SubtypeInk or SubtypePolygon or SubtypePolyline => MoveStrokes(annotation, subtype, kind, map),
            SubtypeSquare or SubtypeCircle or SubtypeText => MoveRegenerated(annotation, map),
            _ => MoveAppearance(annotation, map),
        };
    }

    /// <summary>Drops an annotation's appearance, so PDFium draws it again or new page objects start a fresh one.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when dropped.</returns>
    private static bool ClearAppearance(nint annotation) => NativeMethods.FPDFAnnot_SetAP(annotation, AppearanceNormal, null) != 0;

    /// <summary>Determines whether an annotation is marked removed, without reading its value.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> when removed.</returns>
    private static bool IsRemoved(nint annotation)
    {
        fixed (byte* key = RemovedKey)
        {
            return (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, key, null, default).Value > sizeof(char);
        }
    }

    /// <summary>Determines whether an annotation may be moved or restyled: on the page, not a reply, and not text markup.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind.</param>
    /// <returns><see langword="true"/> when it may be changed.</returns>
    private static bool IsEditable(nint annotation, AnnotationKind kind) =>
        kind is not (AnnotationKind.Highlight or AnnotationKind.Underline or AnnotationKind.StrikeOut or AnnotationKind.Squiggly)
        && !IsRemoved(annotation)
        && !IsReply(annotation);

    /// <summary>Moves a rectangle, ellipse or note, which PDFium draws again from its rectangle.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="map">The move.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private static bool MoveRegenerated(nint annotation, in RectMap map)
    {
        if (NativeMethods.FPDFAnnot_SetRect(annotation, map.Target) == 0)
        {
            return false;
        }

        _ = NativeMethods.FPDFAnnot_SetAP(annotation, AppearanceNormal, null);
        return true;
    }

    /// <summary>
    /// Moves an annotation whose appearance is kept, such as a stamp or text box: readers fit the appearance's box to the
    /// rectangle. A callout's leader line points written for saving move with it.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="map">The move.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private static bool MoveAppearance(nint annotation, in RectMap map)
    {
        if (NativeMethods.FPDFAnnot_SetRect(annotation, map.Target) == 0)
        {
            return false;
        }

        var entries = ReadString(annotation, EntriesKey);
        if (entries.Length > 0)
        {
            WriteEntries(annotation, entries, map);
            MoveAnchor(annotation, map);
        }

        return true;
    }

    /// <summary>Moves and redraws a drawing, line, arrow or polygon point by point.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its subtype.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="map">The move, or <see cref="RectMap.Identity"/> to redraw in place.</param>
    /// <returns><see langword="true"/> when moved.</returns>
    private static bool MoveStrokes(nint annotation, int subtype, AnnotationKind kind, in RectMap map)
    {
        var path = new PathBuffer();
        try
        {
            ReadStrokes(annotation, subtype, ref path);
            if (path.PointCount == 0)
            {
                return false;
            }

            map.Apply(path.Points);
            var width = GetLineWidth(annotation);
            if (kind == AnnotationKind.Arrow)
            {
                ShapeArrowHead(path.Points, path.Lengths, width);
            }

            WriteStrokes(annotation, subtype, kind, ref path);
            return Redraw(annotation, kind, ref path, GetColor(annotation, kind), width);
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Draws a drawing, line, arrow or polygon again in a colour and width, reading its points.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its subtype.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <returns><see langword="true"/> when drawn.</returns>
    private static bool Redraw(nint annotation, int subtype, uint color, float width)
    {
        var path = new PathBuffer();
        try
        {
            ReadStrokes(annotation, subtype, ref path);
            return path.PointCount > 0 && Redraw(annotation, GetKind(annotation) ?? AnnotationKind.Ink, ref path, color, width);
        }
        finally
        {
            path.Dispose();
        }
    }

    /// <summary>Sizes the annotation to its points and writes their appearance stream.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="kind">Its kind, which decides how the points are joined.</param>
    /// <param name="path">The points in PDF space.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <returns><see langword="true"/> when drawn.</returns>
    private static bool Redraw(nint annotation, AnnotationKind kind, ref PathBuffer path, uint color, float width)
    {
        if (width <= 0)
        {
            width = DefaultLineWidth;
        }

        PdfBounds bounds = default;
        foreach (var point in path.Points)
        {
            bounds.Add(point.X, point.Y);
        }

        var margin = kind == AnnotationKind.Cloud ? CloudMargin(width) : width;
        if (NativeMethods.FPDFAnnot_SetRect(annotation, bounds.ToRect(margin)) == 0)
        {
            return false;
        }

        var writer = new AppearanceWriter(AppearanceChars);
        try
        {
            writer.Append("q\n");
            writer.Number(width);
            writer.Append("w 1 J 1 j\n");
            writer.Color(color, "RG\n");
            switch (kind)
            {
                case AnnotationKind.Cloud:
                {
                    WriteCloud(ref writer, path.Points, width);
                    break;
                }

                case AnnotationKind.Polygon:
                {
                    WriteStrokesPath(ref writer, path.Points, path.Lengths);
                    writer.Append("h\n");
                    break;
                }

                default:
                {
                    WriteStrokesPath(ref writer, path.Points, path.Lengths);
                    break;
                }
            }

            writer.Append("S\nQ\n");
            return writer.ApplyAppearance(annotation);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Writes strokes as joined lines; a single point draws a dot with the round cap.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="lengths">The stroke lengths.</param>
    private static void WriteStrokesPath(ref AppearanceWriter writer, ReadOnlySpan<FsPointF> points, ReadOnlySpan<int> lengths)
    {
        var offset = 0;
        foreach (var length in lengths)
        {
            var stroke = points.Slice(offset, length);
            writer.Point(stroke[0].X, stroke[0].Y, "m");
            if (stroke.Length == 1)
            {
                writer.Point(stroke[0].X, stroke[0].Y, "l");
            }

            for (var i = 1; i < stroke.Length; i++)
            {
                writer.Point(stroke[i].X, stroke[i].Y, "l");
            }

            offset += length;
        }
    }

    /// <summary>Reads an annotation's strokes: an ink list, or a polygon's vertices as one stroke.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its subtype.</param>
    /// <param name="path">Receives the points.</param>
    private static void ReadStrokes(nint annotation, int subtype, ref PathBuffer path)
    {
        if (subtype != SubtypeInk)
        {
            ReadVertices(annotation, ref path);
            return;
        }

        var count = (int)NativeMethods.FPDFAnnot_GetInkListCount(annotation).Value;
        for (var i = 0; i < count; i++)
        {
            var index = new CULong((uint)i);
            var length = (int)NativeMethods.FPDFAnnot_GetInkListPath(annotation, index, null, default).Value;
            if (length <= 0)
            {
                continue;
            }

            var stroke = path.BeginStroke(length);
            fixed (FsPointF* points = stroke)
            {
                length = (int)NativeMethods.FPDFAnnot_GetInkListPath(annotation, index, points, new((uint)length)).Value;
            }

            path.EndStroke(Math.Min(length, stroke.Length));
        }
    }

    /// <summary>Rewrites an annotation's strokes after they moved, and the vertices written for saving a polygon.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its subtype.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="path">The points in PDF space.</param>
    private static void WriteStrokes(nint annotation, int subtype, AnnotationKind kind, ref PathBuffer path)
    {
        if (subtype == SubtypeInk)
        {
            _ = NativeMethods.FPDFAnnot_RemoveInkList(annotation);
            var offset = 0;
            foreach (var length in path.Lengths)
            {
                fixed (FsPointF* points = path.Points.Slice(offset, length))
                {
                    _ = NativeMethods.FPDFAnnot_AddInkStroke(annotation, points, (nuint)length);
                }

                offset += length;
            }
        }

        if (kind is AnnotationKind.Polygon or AnnotationKind.Cloud or AnnotationKind.PolyLine)
        {
            WritePolygonEntries(annotation, subtype, kind, path.Points);
        }
    }

    /// <summary>Rebuilds an arrow's head from its shaft (the first stroke), sized to the line width.</summary>
    /// <param name="points">The arrow's points: the shaft, then the two sides of the head.</param>
    /// <param name="lengths">The stroke lengths.</param>
    /// <param name="width">The line width.</param>
    private static void ShapeArrowHead(Span<FsPointF> points, ReadOnlySpan<int> lengths, float width)
    {
        if (lengths.Length != ArrowStrokes || points.Length != ArrowPoints)
        {
            return;
        }

        var tail = points[0];
        var tip = points[1];
        var angle = Math.Atan2(tail.Y - tip.Y, tail.X - tip.X);
        var head = Math.Max(MinArrowHead, ArrowHeadWidths * width);
        points[2] = tip;
        points[3] = new((float)(tip.X + (Math.Cos(angle + ArrowHeadAngle) * head)), (float)(tip.Y + (Math.Sin(angle + ArrowHeadAngle) * head)));
        points[4] = tip;
        points[5] = new((float)(tip.X + (Math.Cos(angle - ArrowHeadAngle) * head)), (float)(tip.Y + (Math.Sin(angle - ArrowHeadAngle) * head)));
    }

    /// <summary>Reads an annotation's line width.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The width in points, or 0 when it has no border.</returns>
    private static float GetLineWidth(nint annotation) =>
        NativeMethods.FPDFAnnot_GetBorder(annotation, out _, out _, out var width) != 0 ? width : 0;

    /// <summary>Reads a number stored as a string value, without allocating.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <returns>The number, or 0 when absent.</returns>
    private static float ReadNumber(nint annotation, ReadOnlySpan<byte> key)
    {
        Span<byte> buffer = stackalloc byte[NumberChars * sizeof(char)];
        int length;
        fixed (byte* keyPointer = key)
        {
            fixed (byte* pointer = buffer)
            {
                length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, keyPointer, pointer, new((uint)buffer.Length)).Value;
            }
        }

        if (length <= sizeof(char) || length > buffer.Length)
        {
            return 0;
        }

        var text = MemoryMarshal.Cast<byte, char>(buffer[..(length - sizeof(char))]);
        return float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }

    /// <summary>Stores a number as a string value, formatted on the stack.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <param name="value">The number.</param>
    /// <returns><see langword="true"/> when stored.</returns>
    private static bool SetNumber(nint annotation, ReadOnlySpan<byte> key, float value)
    {
        Span<char> text = stackalloc char[NumberChars];
        if (!value.TryFormat(text, out var written, "0.###", CultureInfo.InvariantCulture) || written >= text.Length)
        {
            return false;
        }

        text[written] = '\0';
        fixed (byte* keyPointer = key)
        {
            fixed (char* valuePointer = text)
            {
                return NativeMethods.FPDFAnnot_SetStringValue(annotation, keyPointer, valuePointer) != 0;
            }
        }
    }

    /// <summary>Reads the modification date, parsed on the stack.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The date, or <see langword="null"/>.</returns>
    private static DateTimeOffset? ReadModified(nint annotation)
    {
        Span<byte> buffer = stackalloc byte[DateChars * sizeof(char)];
        int length;
        fixed (byte* key = "M"u8)
        {
            fixed (byte* pointer = buffer)
            {
                length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, key, pointer, new((uint)buffer.Length)).Value;
            }
        }

        return length <= sizeof(char) || length > buffer.Length ? null : PdfDate.Parse(MemoryMarshal.Cast<byte, char>(buffer[..(length - sizeof(char))]));
    }

    /// <summary>A move from one PDF rectangle to another, scaling each axis.</summary>
    /// <param name="Source">The rectangle before.</param>
    /// <param name="Target">The rectangle after.</param>
    private readonly record struct RectMap(FsRectF Source, FsRectF Target)
    {
        /// <summary>Gets a move that leaves points where they are.</summary>
        public static RectMap Identity { get; } = new(new(0, 1, 1, 0), new(0, 1, 1, 0));

        /// <summary>Gets the horizontal scale.</summary>
        private float ScaleX => Source.Right - Source.Left > MinSide ? Math.Max(MinSide, Target.Right - Target.Left) / (Source.Right - Source.Left) : 1;

        /// <summary>Gets the vertical scale.</summary>
        private float ScaleY => Source.Top - Source.Bottom > MinSide ? Math.Max(MinSide, Target.Top - Target.Bottom) / (Source.Top - Source.Bottom) : 1;

        /// <summary>Moves points.</summary>
        /// <param name="points">The points, moved in place.</param>
        public void Apply(Span<FsPointF> points)
        {
            var scaleX = ScaleX;
            var scaleY = ScaleY;
            for (var i = 0; i < points.Length; i++)
            {
                points[i] = new(Target.Left + ((points[i].X - Source.Left) * scaleX), Target.Bottom + ((points[i].Y - Source.Bottom) * scaleY));
            }
        }

        /// <summary>Moves one point.</summary>
        /// <param name="x">The x coordinate.</param>
        /// <param name="y">The y coordinate.</param>
        /// <returns>The moved point.</returns>
        public FsPointF Apply(float x, float y) => new(Target.Left + ((x - Source.Left) * ScaleX), Target.Bottom + ((y - Source.Bottom) * ScaleY));
    }
}
