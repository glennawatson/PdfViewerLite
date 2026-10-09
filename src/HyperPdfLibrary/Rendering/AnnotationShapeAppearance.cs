// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Draws shape annotation appearances.</summary>
internal static class AnnotationShapeAppearance
{
    /// <summary>The control point factor PDFium uses for a quarter ellipse.</summary>
    private const float Kappa = 0.5523F;

    /// <summary>The points a cloudy ellipse is approximated with.</summary>
    private const int CloudEllipsePoints = 16;

    /// <summary>The numbers in a /RD entry.</summary>
    private const int DifferenceCount = 4;

    /// <summary>The position of the right inset in /RD.</summary>
    private const int DifferenceRight = 2;

    /// <summary>The position of the top inset in /RD.</summary>
    private const int DifferenceTop = 3;

    /// <summary>The numbers in a /L entry.</summary>
    private const int LineNumbers = 4;

    /// <summary>The position of the end x in /L.</summary>
    private const int LineEndX = 2;

    /// <summary>The position of the end y in /L.</summary>
    private const int LineEndY = 3;

    /// <summary>The length of an arrow head's sides as a multiple of the line width.</summary>
    private const float ArrowWidths = 6;

    /// <summary>The shortest arrow head side, in points.</summary>
    private const float MinArrow = 4;

    /// <summary>The coordinates in one point.</summary>
    private const int PointFields = 2;

    /// <summary>Draws a Square annotation: its interior colour and border, deflated by half the border as PDFium does.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance? DrawSquare(AnnotationContext context)
    {
        var annotation = context.Annotation;
        var rect = AnnotationAppearanceGeometry.Rect(annotation);
        var builder = default(PdfContentBuilder);
        try
        {
            var width = BeginShape(ref builder, annotation);
            if (IsCloudy(annotation))
            {
                var inner = CloudInterior(annotation, rect, width);
                Span<Vector2> corners = [new(inner.Left, inner.Bottom), new(inner.Right, inner.Bottom), new(inner.Right, inner.Top), new(inner.Left, inner.Top)];
                PdfAppearances.AddCloud(ref builder, corners, width);
            }
            else
            {
                var inner = width > 0 ? AnnotationAppearanceGeometry.Deflate(rect, width * AnnotationAppearanceGeometry.Half) : rect;
                builder.Rectangle(inner.Left, inner.Bottom, inner.Width, inner.Height);
            }

            Paint(ref builder, width > 0, annotation.GetArray(KnownName.IC) is { Count: > 0 });
            return AnnotationAppearanceResources.Finish(ref builder, context, rect, AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Draws a Circle annotation: an ellipse of four curves inside the deflated rectangle.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance.</returns>
    internal static GeneratedAppearance? DrawCircle(AnnotationContext context)
    {
        var annotation = context.Annotation;
        var rect = AnnotationAppearanceGeometry.Rect(annotation);
        var builder = default(PdfContentBuilder);
        try
        {
            var width = BeginShape(ref builder, annotation);
            if (IsCloudy(annotation))
            {
                AddCloudyEllipse(ref builder, CloudInterior(annotation, rect, width), width);
            }
            else
            {
                AddEllipse(ref builder, width > 0 ? AnnotationAppearanceGeometry.Deflate(rect, width * AnnotationAppearanceGeometry.Half) : rect);
            }

            Paint(ref builder, width > 0, annotation.GetArray(KnownName.IC) is { Count: > 0 });
            return AnnotationAppearanceResources.Finish(ref builder, context, rect, AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Draws an Ink annotation: each stroke of /InkList, in a rectangle grown by half the line as PDFium does.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without strokes or a line.</returns>
    internal static GeneratedAppearance? DrawInk(AnnotationContext context)
    {
        var annotation = context.Annotation;
        var width = AnnotationAppearanceBorder.BorderWidth(annotation);
        if (annotation.GetArray(KnownName.InkList) is not { Count: > 0 } strokes || width <= 0)
        {
            return null;
        }

        var builder = default(PdfContentBuilder);
        try
        {
            AnnotationAppearanceContent.Begin(ref builder);
            AnnotationAppearanceContent.WriteColor(ref builder, annotation.GetArray(KnownName.C), 0, true);
            builder.SetLineWidth(width);
            AnnotationAppearanceBorder.WriteDash(ref builder, annotation);
            for (var i = 0; i < strokes.Count; i++)
            {
                AddInkStroke(ref builder, strokes.GetArray(i));
            }

            var rect = AnnotationAppearanceGeometry.Deflate(AnnotationAppearanceGeometry.Rect(annotation), -width * AnnotationAppearanceGeometry.Half);
            return AnnotationAppearanceResources.Finish(ref builder, context, rect, AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Draws a Line annotation from /L, with open or closed arrow line endings from /LE.</summary>
    /// <param name="context">The annotation.</param>
    /// <returns>The appearance, or null without a line.</returns>
    internal static GeneratedAppearance? DrawLine(AnnotationContext context)
    {
        var annotation = context.Annotation;
        if (annotation.GetArray(KnownName.L) is not { Count: >= LineNumbers } line)
        {
            return null;
        }

        var start = new Vector2(line.GetSingle(0), line.GetSingle(1));
        var end = new Vector2(line.GetSingle(LineEndX), line.GetSingle(LineEndY));
        var builder = default(PdfContentBuilder);
        try
        {
            var width = BeginShape(ref builder, annotation);
            builder.MoveTo(start.X, start.Y);
            builder.LineTo(end.X, end.Y);
            builder.Stroke();
            var endings = annotation.GetArray(KnownName.LE);
            AddLineEnding(ref builder, endings?.GetName(0).ToKnownName() ?? KnownName.None, start, end, width);
            AddLineEnding(ref builder, endings?.GetName(1).ToKnownName() ?? KnownName.None, end, start, width);
            return AnnotationAppearanceResources.Finish(ref builder, context, AnnotationAppearanceGeometry.Rect(annotation), AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Draws a Polygon (closed, filled with /IC) or PolyLine (open) annotation from /Vertices.</summary>
    /// <param name="context">The annotation.</param>
    /// <param name="closed">Whether the shape is a polygon.</param>
    /// <returns>The appearance, or null without vertices.</returns>
    internal static GeneratedAppearance? DrawPolygon(AnnotationContext context, bool closed)
    {
        var annotation = context.Annotation;
        if (annotation.GetArray(KnownName.Vertices) is not { Count: >= LineNumbers } vertices)
        {
            return null;
        }

        var points = new Vector2[vertices.Count / PointFields];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new(vertices.GetSingle(i * PointFields), vertices.GetSingle((i * PointFields) + 1));
        }

        var builder = default(PdfContentBuilder);
        try
        {
            var width = BeginShape(ref builder, annotation);
            AddPolygonPath(ref builder, annotation, points, closed, width);
            Paint(ref builder, width > 0, closed && annotation.GetArray(KnownName.IC) is { Count: > 0 });
            return AnnotationAppearanceResources.Finish(ref builder, context, AnnotationAppearanceGeometry.Rect(annotation), AnnotationAppearanceResources.CreateResources(context, KnownName.Normal));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Selects the graphics state, interior and border colours, border width and dash of a shape.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The border width.</returns>
    private static float BeginShape(ref PdfContentBuilder builder, PdfDictionary annotation)
    {
        AnnotationAppearanceContent.Begin(ref builder);
        AnnotationAppearanceContent.WriteColor(ref builder, annotation.GetArray(KnownName.IC), null, false);
        AnnotationAppearanceContent.WriteColor(ref builder, annotation.GetArray(KnownName.C), 0, true);
        var width = AnnotationAppearanceBorder.BorderWidth(annotation);
        if (width > 0)
        {
            builder.SetLineWidth(width);
            AnnotationAppearanceBorder.WriteDash(ref builder, annotation);
        }

        return width;
    }

    /// <summary>Paints the path with the operator PDFium picks: close, fill and stroke; close and stroke; fill; or nothing.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="stroke">Whether the border is drawn.</param>
    /// <param name="fill">Whether the interior is filled.</param>
    private static void Paint(ref PdfContentBuilder builder, bool stroke, bool fill)
    {
        if (stroke && fill)
        {
            builder.CloseFillAndStroke();
        }
        else if (stroke)
        {
            builder.CloseAndStroke();
        }
        else if (fill)
        {
            builder.Fill();
        }
        else
        {
            builder.EndPath();
        }
    }

    /// <summary>Determines whether an annotation asks for a cloudy border (<c>/BE &lt;&lt; /S /C &gt;&gt;</c>).</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns><see langword="true"/> for a cloudy border.</returns>
    private static bool IsCloudy(PdfDictionary annotation) =>
        annotation.GetDictionary(KnownName.BE) is { } effect && effect.IsName(KnownName.S, KnownName.C);

    /// <summary>Gets the rectangle a cloud is drawn round: /Rect less /RD, or less the cloud's own reach.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="rect">The annotation rectangle.</param>
    /// <param name="width">The border width.</param>
    /// <returns>The inner rectangle.</returns>
    private static PdfRectangle CloudInterior(PdfDictionary annotation, PdfRectangle rect, float width) =>
        annotation.GetArray(KnownName.RD) is { Count: DifferenceCount } inset
            ? new(rect.Left + inset.GetSingle(0), rect.Bottom + inset.GetSingle(1), rect.Right - inset.GetSingle(DifferenceRight), rect.Top - inset.GetSingle(DifferenceTop))
            : AnnotationAppearanceGeometry.Deflate(rect, PdfAppearances.CloudMargin(width));

    /// <summary>Adds an ellipse of four curves filling a rectangle, as PDFium writes it.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="rect">The rectangle.</param>
    private static void AddEllipse(ref PdfContentBuilder builder, PdfRectangle rect)
    {
        var middleX = (rect.Left + rect.Right) * AnnotationAppearanceGeometry.Half;
        var middleY = (rect.Top + rect.Bottom) * AnnotationAppearanceGeometry.Half;
        var deltaX = Kappa * rect.Width * AnnotationAppearanceGeometry.Half;
        var deltaY = Kappa * rect.Height * AnnotationAppearanceGeometry.Half;
        builder.MoveTo(middleX, rect.Top);
        builder.CurveTo(middleX + deltaX, rect.Top, rect.Right, middleY + deltaY, rect.Right, middleY);
        builder.CurveTo(rect.Right, middleY - deltaY, middleX + deltaX, rect.Bottom, middleX, rect.Bottom);
        builder.CurveTo(middleX - deltaX, rect.Bottom, rect.Left, middleY - deltaY, rect.Left, middleY);
        builder.CurveTo(rect.Left, middleY + deltaY, middleX - deltaX, rect.Top, middleX, rect.Top);
    }

    /// <summary>Adds a cloudy border round an ellipse, approximated by a polygon of its points.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="rect">The ellipse's rectangle.</param>
    /// <param name="width">The border width.</param>
    private static void AddCloudyEllipse(ref PdfContentBuilder builder, PdfRectangle rect, float width)
    {
        Span<Vector2> points = stackalloc Vector2[CloudEllipsePoints];
        var centre = new Vector2((rect.Left + rect.Right) * AnnotationAppearanceGeometry.Half, (rect.Top + rect.Bottom) * AnnotationAppearanceGeometry.Half);
        for (var i = 0; i < points.Length; i++)
        {
            var angle = MathF.Tau * i / points.Length;
            points[i] = centre + new Vector2(MathF.Cos(angle) * rect.Width * AnnotationAppearanceGeometry.Half, MathF.Sin(angle) * rect.Height * AnnotationAppearanceGeometry.Half);
        }

        PdfAppearances.AddCloud(ref builder, points, width);
    }

    /// <summary>Adds one ink stroke the way PDFium writes it: a move to the first point, a line through every point, then a stroke.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="points">The stroke's coordinates, or null.</param>
    private static void AddInkStroke(ref PdfContentBuilder builder, PdfArray? points)
    {
        if (points is not { Count: >= PointFields })
        {
            return;
        }

        builder.MoveTo(points.GetSingle(0), points.GetSingle(1));
        for (var j = 0; j < points.Count - 1; j += PointFields)
        {
            builder.LineTo(points.GetSingle(j), points.GetSingle(j + 1));
        }

        builder.Stroke();
    }

    /// <summary>Adds a polygon's or polyline's path, with a cloudy border for a polygon that asks for one.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="points">The vertices.</param>
    /// <param name="closed">Whether the shape is closed.</param>
    /// <param name="width">The border width.</param>
    private static void AddPolygonPath(ref PdfContentBuilder builder, PdfDictionary annotation, ReadOnlySpan<Vector2> points, bool closed, float width)
    {
        if (closed && IsCloudy(annotation))
        {
            PdfAppearances.AddCloud(ref builder, points, width);
            return;
        }

        builder.MoveTo(points[0].X, points[0].Y);
        for (var i = 1; i < points.Length; i++)
        {
            builder.LineTo(points[i].X, points[i].Y);
        }

        if (closed)
        {
            builder.ClosePath();
        }
    }

    /// <summary>Adds an open or closed arrow at one end of a line.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="ending">The /LE name.</param>
    /// <param name="tip">The end the arrow is at.</param>
    /// <param name="from">The other end.</param>
    /// <param name="width">The line width.</param>
    private static void AddLineEnding(ref PdfContentBuilder builder, KnownName ending, Vector2 tip, Vector2 from, float width)
    {
        if (ending is not (KnownName.OpenArrow or KnownName.ClosedArrow))
        {
            return;
        }

        var head = PdfAppearances.OpenArrowHead(tip, from, Math.Max(MinArrow, width * ArrowWidths));
        builder.MoveTo(head.First.X, head.First.Y);
        builder.LineTo(tip.X, tip.Y);
        builder.LineTo(head.Second.X, head.Second.Y);
        if (ending == KnownName.ClosedArrow)
        {
            builder.CloseAndStroke();
        }
        else
        {
            builder.Stroke();
        }
    }
}
