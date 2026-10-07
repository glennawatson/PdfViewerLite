// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Polygons, clouds and runs of lines. PDFium cannot create Polygon or PolyLine annotations, so they are made as ink
/// (whose points PDFium can store) with an appearance stream drawn here, and their standard entries (<c>/Subtype</c>,
/// <c>/Vertices</c> and, for a cloud, <c>/IT /PolygonCloud</c> with a cloudy border effect) are written when the file is
/// saved. Files from other programs keep their Polygon and PolyLine annotations, read through their vertices.
/// </summary>
internal static unsafe partial class PdfiumAnnotations
{
    /// <summary>PDFium's Polygon subtype.</summary>
    private const int SubtypePolygon = 7;

    /// <summary>PDFium's PolyLine subtype.</summary>
    private const int SubtypePolyline = 8;

    /// <summary>The subject marking a polygon.</summary>
    private const string PolygonSubject = "Polygon";

    /// <summary>The subject marking a cloud.</summary>
    private const string CloudSubject = "Cloud";

    /// <summary>The subject marking a run of lines.</summary>
    private const string PolyLineSubject = "Polyline";

    /// <summary>The fewest points of a closed shape.</summary>
    private const int MinPolygonPoints = 3;

    /// <summary>The fewest points of a run of lines.</summary>
    private const int MinPolyLinePoints = 2;

    /// <summary>The shortest scallop of a cloud, in points.</summary>
    private const float MinScallop = 9;

    /// <summary>A cloud's scallop length as a multiple of its line width.</summary>
    private const float ScallopWidths = 5;

    /// <summary>How far a scallop's curve handles reach out, as a part of its length: four thirds of its radius makes a near half circle.</summary>
    private const float ScallopBulge = 0.667F;

    /// <summary>The shortest edge, in points, drawn with scallops.</summary>
    private const float MinEdge = 0.01F;

    /// <summary>The characters expected in a polygon's entries.</summary>
    private const int EntriesChars = 256;

    /// <summary>The numbers in a point.</summary>
    private const int Coordinates = 2;

    /// <summary>The text between an array's key and its first number: a space and the opening bracket.</summary>
    private const string ArrayOpening = " [";

    /// <summary>Gets the key of the PDF type naming a cloud's intent.</summary>
    private static ReadOnlySpan<byte> IntentKey => "IT"u8;

    /// <summary>Draws a polygon, cloud or run of lines.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="vertices">The corners in page space.</param>
    /// <param name="color">The colour.</param>
    /// <param name="width">The line width.</param>
    /// <param name="author">The author recorded.</param>
    /// <returns>The annotation index, or -1.</returns>
    internal static int AddPolygon(PdfiumPage page, AnnotationKind kind, ReadOnlySpan<PagePoint> vertices, uint color, float width, string author)
    {
        var (subject, fewest) = kind switch
        {
            AnnotationKind.Polygon => (PolygonSubject, MinPolygonPoints),
            AnnotationKind.Cloud => (CloudSubject, MinPolygonPoints),
            AnnotationKind.PolyLine => (PolyLineSubject, MinPolyLinePoints),
            _ => (string.Empty, int.MaxValue),
        };
        if (vertices.Length < fewest || width <= 0)
        {
            return -1;
        }

        var annotation = NativeMethods.FPDFPage_CreateAnnot(page.Handle, SubtypeInk);
        if (annotation == 0)
        {
            return -1;
        }

        var path = new PathBuffer();
        try
        {
            var points = path.BeginStroke(vertices.Length);
            PdfBounds bounds = default;
            for (var i = 0; i < vertices.Length; i++)
            {
                page.ToPdf(vertices[i], out var x, out var y);
                bounds.Add(x, y);
                points[i] = new((float)x, (float)y);
            }

            path.EndStroke(vertices.Length);
            _ = NativeMethods.FPDFAnnot_SetBorder(annotation, 0, 0, width);
            Finish(annotation, bounds.ToRect(width), color, string.Empty, subject, author);
            WriteStrokes(annotation, SubtypeInk, kind, ref path);
            return Redraw(annotation, kind, ref path, color, width) ? NativeMethods.FPDFPage_GetAnnotIndex(page.Handle, annotation) : -1;
        }
        finally
        {
            path.Dispose();
            NativeMethods.FPDFPage_CloseAnnot(annotation);
        }
    }

    /// <summary>Tells polygons, clouds and runs of lines drawn as ink here apart from freehand ink.</summary>
    /// <param name="annotation">The ink annotation.</param>
    /// <returns>The kind, or <see langword="null"/> for plain ink.</returns>
    private static AnnotationKind? GetPolygonKind(nint annotation)
    {
        if (HasSubject(annotation, PolygonSubject))
        {
            return AnnotationKind.Polygon;
        }

        if (HasSubject(annotation, CloudSubject))
        {
            return AnnotationKind.Cloud;
        }

        return HasSubject(annotation, PolyLineSubject) ? AnnotationKind.PolyLine : null;
    }

    /// <summary>Tells a cloud from a plain polygon in a Polygon annotation: by the subject written here, or the cloud intent.</summary>
    /// <param name="annotation">The polygon.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind GetSavedPolygonKind(nint annotation) =>
        HasSubject(annotation, CloudSubject) || HasValue(annotation, IntentKey, "PolygonCloud") ? AnnotationKind.Cloud : AnnotationKind.Polygon;

    /// <summary>Reads a polygon's vertices: the ones written for saving when it has moved, otherwise the file's.</summary>
    /// <param name="annotation">The polygon or polyline.</param>
    /// <param name="path">Receives the vertices as one stroke.</param>
    private static void ReadVertices(nint annotation, ref PathBuffer path)
    {
        var entries = ReadString(annotation, EntriesKey);
        if (entries.Length > 0 && ReadArray(entries, "/Vertices", ref path))
        {
            return;
        }

        var count = (int)NativeMethods.FPDFAnnot_GetVertices(annotation, null, default).Value;
        if (count <= 0)
        {
            return;
        }

        var stroke = path.BeginStroke(count);
        fixed (FsPointF* points = stroke)
        {
            count = (int)NativeMethods.FPDFAnnot_GetVertices(annotation, points, new((uint)count)).Value;
        }

        path.EndStroke(Math.Min(count, stroke.Length));
    }

    /// <summary>Reads pairs of numbers from an array written into pending entries, as one stroke.</summary>
    /// <param name="entries">The entries text.</param>
    /// <param name="key">The array's key with its slash, such as <c>/Vertices</c>.</param>
    /// <param name="path">Receives the points.</param>
    /// <returns><see langword="true"/> when the array was found.</returns>
    private static bool ReadArray(string entries, string key, ref PathBuffer path)
    {
        var at = entries.IndexOf($"{key}{ArrayOpening}", StringComparison.Ordinal);
        if (at < 0)
        {
            return false;
        }

        var start = at + key.Length + ArrayOpening.Length;
        var end = entries.IndexOf(']', start);
        if (end < 0)
        {
            return false;
        }

        var numbers = entries.AsSpan(start, end - start);
        var count = 0;
        foreach (var _ in numbers.Split(' '))
        {
            count++;
        }

        var stroke = path.BeginStroke(count / Coordinates);
        var written = 0;
        var x = 0F;
        var odd = false;
        foreach (var range in numbers.Split(' '))
        {
            if (!float.TryParse(numbers[range], NumberStyles.Float, CultureInfo.InvariantCulture, out var value))
            {
                continue;
            }

            if (!odd)
            {
                x = value;
            }
            else if (written < stroke.Length)
            {
                stroke[written] = new(x, value);
                written++;
            }

            odd = !odd;
        }

        path.EndStroke(written);
        return written > 0;
    }

    /// <summary>
    /// Writes the entries a polygon needs when the file is saved: its type when it is still ink, its vertices, and for a
    /// cloud its intent and cloudy border.
    /// </summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="subtype">Its subtype now.</param>
    /// <param name="kind">Its kind.</param>
    /// <param name="vertices">The vertices in PDF space.</param>
    private static void WritePolygonEntries(nint annotation, int subtype, AnnotationKind kind, ReadOnlySpan<FsPointF> vertices)
    {
        var writer = new AppearanceWriter(EntriesChars);
        try
        {
            if (subtype == SubtypeInk)
            {
                writer.Append(kind == AnnotationKind.PolyLine ? "/Subtype /PolyLine " : "/Subtype /Polygon ");
            }

            writer.Append("/Vertices [");
            foreach (var point in vertices)
            {
                writer.Number(point.X);
                writer.Number(point.Y);
            }

            writer.Append("]");
            if (kind == AnnotationKind.Cloud)
            {
                writer.Append(" /IT /PolygonCloud /BE << /S /C /I 1 >>");
            }

            _ = writer.ApplyString(annotation, EntriesKey);
        }
        finally
        {
            writer.Dispose();
        }
    }

    /// <summary>Moves the point arrays of pending entries, such as a callout's leader line, with the annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="entries">The entries text.</param>
    /// <param name="map">The move.</param>
    private static void WriteEntries(nint annotation, string entries, in RectMap map)
    {
        var at = entries.IndexOf("/CL [", StringComparison.Ordinal);
        var end = at < 0 ? -1 : entries.IndexOf(']', at);
        if (end < 0)
        {
            return;
        }

        var path = new PathBuffer();
        var writer = new AppearanceWriter(entries.Length + EntriesChars);
        try
        {
            _ = ReadArray(entries, "/CL", ref path);
            map.Apply(path.Points);
            writer.Append(entries.AsSpan(0, at));
            writer.Append("/CL [");
            foreach (var point in path.Points)
            {
                writer.Number(point.X);
                writer.Number(point.Y);
            }

            writer.Append(entries.AsSpan(end));
            _ = writer.ApplyString(annotation, EntriesKey);
        }
        finally
        {
            writer.Dispose();
            path.Dispose();
        }
    }

    /// <summary>
    /// Writes a cloud: each side of the polygon becomes a row of outward scallops, each a cubic curve close to a half
    /// circle, the way PDF readers draw a cloudy border.
    /// </summary>
    /// <param name="writer">The writer.</param>
    /// <param name="vertices">The corners in PDF space.</param>
    /// <param name="width">The line width.</param>
    private static void WriteCloud(ref AppearanceWriter writer, ReadOnlySpan<FsPointF> vertices, float width)
    {
        var area = 0D;
        for (var i = 0; i < vertices.Length; i++)
        {
            var next = vertices[(i + 1) % vertices.Length];
            area += (vertices[i].X * next.Y) - (next.X * vertices[i].Y);
        }

        // Outward is to the right of each side when the corners run anticlockwise (PDF's y grows upwards).
        var outward = area > 0 ? 1D : -1D;
        var scallop = Math.Max(MinScallop, ScallopWidths * width);
        writer.Point(vertices[0].X, vertices[0].Y, "m");
        for (var i = 0; i < vertices.Length; i++)
        {
            var from = vertices[i];
            var to = vertices[(i + 1) % vertices.Length];
            var dx = to.X - from.X;
            var dy = to.Y - from.Y;
            var length = Math.Sqrt((dx * dx) + (dy * dy));
            if (length < MinEdge)
            {
                continue;
            }

            var steps = Math.Max(1, (int)Math.Round(length / scallop));
            var step = length / steps;
            var (ux, uy) = (dx / length, dy / length);
            var bulge = step * ScallopBulge;
            var (nx, ny) = (uy * outward * bulge, -ux * outward * bulge);
            for (var k = 0; k < steps; k++)
            {
                var x0 = from.X + (ux * step * k);
                var y0 = from.Y + (uy * step * k);
                var x1 = x0 + (ux * step);
                var y1 = y0 + (uy * step);
                writer.Number(x0 + nx);
                writer.Number(y0 + ny);
                writer.Number(x1 + nx);
                writer.Number(y1 + ny);
                writer.Point(x1, y1, "c");
            }
        }

        writer.Append("h\n");
    }

    /// <summary>Gets how far a cloud's scallops reach past its corners, plus its line.</summary>
    /// <param name="width">The line width.</param>
    /// <returns>The margin in points.</returns>
    private static float CloudMargin(float width) => (Math.Max(MinScallop, ScallopWidths * width) * ScallopBulge) + width;

    /// <summary>Determines whether a name or string value equals a text, without allocating.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="key">The null terminated key.</param>
    /// <param name="value">The text.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    private static bool HasValue(nint annotation, ReadOnlySpan<byte> key, string value)
    {
        Span<byte> buffer = stackalloc byte[ReadBufferBytes];
        int length;
        fixed (byte* keyPointer = key)
        {
            fixed (byte* pointer = buffer)
            {
                length = (int)NativeMethods.FPDFAnnot_GetStringValue(annotation, keyPointer, pointer, new((uint)buffer.Length)).Value;
            }
        }

        return length > sizeof(char) && length <= buffer.Length && System.Runtime.InteropServices.MemoryMarshal.Cast<byte, char>(buffer[..(length - sizeof(char))]).SequenceEqual(value);
    }
}
