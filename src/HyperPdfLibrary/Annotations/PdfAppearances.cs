// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Draws the shapes annotation appearances are made of: colours, joined strokes, closed polygons, cloudy borders
/// (PDF 32000-2, 12.5.4, <c>/BE /S /C</c>) and open arrow line endings. Points are in the appearance's space.
/// </summary>
public static class PdfAppearances
{
    /// <summary>The largest colour channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The round line cap and round line join.</summary>
    private const int Round = 1;

    /// <summary>The shortest scallop of a cloud, in points.</summary>
    private const float MinScallop = 9;

    /// <summary>A cloud's scallop length as a multiple of its line width.</summary>
    private const float ScallopWidths = 5;

    /// <summary>How far a scallop's curve handles reach out, as a part of its length: close to a half circle.</summary>
    private const float ScallopBulge = 0.667F;

    /// <summary>The shortest side, in points, drawn with scallops.</summary>
    private const float MinSide = 0.01F;

    /// <summary>The angle between a line and each side of an open arrow head, in radians (about 25 degrees).</summary>
    private const double ArrowAngle = 0.436;

    /// <summary>
    /// Sets both the fill and the stroke colour, so every part of the appearance, and readers that take an
    /// appearance's colour from its first object, see the annotation's colour.
    /// </summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    public static void SetColors(ref PdfContentBuilder builder, uint color)
    {
        var red = ((color >> RedShift) & ChannelMask) / ChannelMax;
        var green = ((color >> GreenShift) & ChannelMask) / ChannelMax;
        var blue = (color & ChannelMask) / ChannelMax;
        builder.SetFillRgb(red, green, blue);
        builder.SetStrokeRgb(red, green, blue);
    }

    /// <summary>Sets a line width with round caps and joins, so strokes keep soft ends and corners.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="width">The line width.</param>
    public static void SetRoundLine(ref PdfContentBuilder builder, float width)
    {
        builder.SetLineWidth(width);
        builder.SetLineCap(Round);
        builder.SetLineJoin(Round);
    }

    /// <summary>Adds strokes as joined lines; a stroke of one point draws a dot with a round cap.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="lengths">The number of points in each stroke.</param>
    public static void AddStrokes(ref PdfContentBuilder builder, scoped ReadOnlySpan<Vector2> points, scoped ReadOnlySpan<int> lengths)
    {
        var offset = 0;
        foreach (var length in lengths)
        {
            if (length <= 0 || offset + length > points.Length)
            {
                return;
            }

            var stroke = points.Slice(offset, length);
            builder.MoveTo(stroke[0].X, stroke[0].Y);
            if (stroke.Length == 1)
            {
                builder.LineTo(stroke[0].X, stroke[0].Y);
            }

            for (var i = 1; i < stroke.Length; i++)
            {
                builder.LineTo(stroke[i].X, stroke[i].Y);
            }

            offset += length;
        }
    }

    /// <summary>Adds a cloudy border round a polygon: each side becomes a row of outward scallops.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="vertices">The corners.</param>
    /// <param name="width">The line width, which sets the scallop size.</param>
    public static void AddCloud(ref PdfContentBuilder builder, scoped ReadOnlySpan<Vector2> vertices, float width)
    {
        if (vertices.IsEmpty)
        {
            return;
        }

        // Outward is to the right of each side when the corners run anticlockwise (y grows upwards).
        var outward = SignedArea(vertices) > 0 ? 1F : -1F;
        var scallop = ScallopLength(width);
        builder.MoveTo(vertices[0].X, vertices[0].Y);
        for (var i = 0; i < vertices.Length; i++)
        {
            AddScallops(ref builder, vertices[i], vertices[(i + 1) % vertices.Length], scallop, outward);
        }

        builder.ClosePath();
    }

    /// <summary>Gets how far a cloud's scallops reach past its corners, plus its line.</summary>
    /// <param name="width">The line width.</param>
    /// <returns>The margin.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float CloudMargin(float width) => (ScallopLength(width) * ScallopBulge) + width;

    /// <summary>Gets the two far ends of an open arrow head at a line's end.</summary>
    /// <param name="tip">The point of the arrow.</param>
    /// <param name="from">A point back along the line.</param>
    /// <param name="length">The length of each side of the head.</param>
    /// <returns>The ends of the two sides.</returns>
    public static PdfArrowHead OpenArrowHead(Vector2 tip, Vector2 from, float length)
    {
        var angle = Math.Atan2(from.Y - tip.Y, from.X - tip.X);
        return new(Toward(tip, angle + ArrowAngle, length), Toward(tip, angle - ArrowAngle, length));
    }

    /// <summary>Gets the point a distance away in a direction.</summary>
    /// <param name="origin">The start.</param>
    /// <param name="angle">The direction in radians.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector2 Toward(Vector2 origin, double angle, double distance) =>
        new((float)(origin.X + (Math.Cos(angle) * distance)), (float)(origin.Y + (Math.Sin(angle) * distance)));

    /// <summary>Gets a cloud's scallop length.</summary>
    /// <param name="width">The line width.</param>
    /// <returns>The length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static float ScallopLength(float width) => Math.Max(MinScallop, ScallopWidths * width);

    /// <summary>Gets twice a polygon's signed area: positive when its corners run anticlockwise.</summary>
    /// <param name="vertices">The corners.</param>
    /// <returns>The signed area, doubled.</returns>
    private static double SignedArea(ReadOnlySpan<Vector2> vertices)
    {
        var area = 0D;
        for (var i = 0; i < vertices.Length; i++)
        {
            var next = vertices[(i + 1) % vertices.Length];
            area += ((double)vertices[i].X * next.Y) - ((double)next.X * vertices[i].Y);
        }

        return area;
    }

    /// <summary>Adds one side of a cloud as scallops, each a cubic curve close to a half circle.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="from">The side's start.</param>
    /// <param name="to">The side's end.</param>
    /// <param name="scallop">The scallop length.</param>
    /// <param name="outward">1 or -1: which side of the line is outside.</param>
    private static void AddScallops(ref PdfContentBuilder builder, Vector2 from, Vector2 to, float scallop, float outward)
    {
        var side = to - from;
        var length = side.Length();
        if (length < MinSide)
        {
            return;
        }

        var steps = Math.Max(1, (int)MathF.Round(length / scallop));
        var step = side / steps;
        var unit = side / length;
        var bulge = length / steps * ScallopBulge;
        var normal = new Vector2(unit.Y * outward * bulge, -unit.X * outward * bulge);
        for (var k = 0; k < steps; k++)
        {
            var start = from + (step * k);
            var end = start + step;
            builder.CurveTo(start.X + normal.X, start.Y + normal.Y, end.X + normal.X, end.Y + normal.Y, end.X, end.Y);
        }
    }
}
