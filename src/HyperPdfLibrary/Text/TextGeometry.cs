// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Matrix and rectangle arithmetic with PDFium's rounding and edge cases, so text decisions match it.</summary>
internal static class TextGeometry
{
    /// <summary>Half, for averaging the two axis scales.</summary>
    private const float Half = 0.5F;

    /// <summary>The difference below which two text state values are the same.</summary>
    private const float SameValueTolerance = 1E-6F;

    /// <summary>Determines whether two text state values are the same, allowing for float rounding.</summary>
    /// <param name="first">The first value.</param>
    /// <param name="second">The second value.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Same(float first, float second) => MathF.Abs(first - second) <= SameValueTolerance;

    /// <summary>Gets the average scale of a matrix's two axes, as PDFium's TransformDistance does.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The scaled distance.</returns>
    internal static float TransformDistance(Matrix3x2 matrix, float distance) =>
        distance * (AxisUnit(matrix.M11, matrix.M12) + AxisUnit(matrix.M22, matrix.M21)) * Half;

    /// <summary>Gets the length of a distance along the x axis after a matrix, as PDFium's TransformXDistance does.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="distance">The distance.</param>
    /// <returns>The transformed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float TransformXDistance(Matrix3x2 matrix, float distance) => MathF.Sqrt((matrix.M11 * distance * matrix.M11 * distance) + (matrix.M12 * distance * matrix.M12 * distance));

    /// <summary>Inverts a matrix, giving the identity when it cannot be inverted, as PDFium does.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <returns>The inverse.</returns>
    internal static Matrix3x2 Invert(Matrix3x2 matrix) => Matrix3x2.Invert(matrix, out var inverse) ? inverse : Matrix3x2.Identity;

    /// <summary>Transforms a point.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="point">The point.</param>
    /// <returns>The transformed point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static Vector2 Transform(Matrix3x2 matrix, Vector2 point) => Vector2.Transform(point, matrix);

    /// <summary>Gets the bounding box of a rectangle after a matrix.</summary>
    /// <param name="matrix">The matrix.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The normalised bounds.</returns>
    internal static PdfRectangle TransformRect(Matrix3x2 matrix, in PdfRectangle rectangle)
    {
        var a = Vector2.Transform(new(rectangle.Left, rectangle.Top), matrix);
        var b = Vector2.Transform(new(rectangle.Left, rectangle.Bottom), matrix);
        var c = Vector2.Transform(new(rectangle.Right, rectangle.Top), matrix);
        var d = Vector2.Transform(new(rectangle.Right, rectangle.Bottom), matrix);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>Normalises a rectangle so left is not past right and bottom is not above top.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The normalised rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRectangle Normalize(in PdfRectangle rectangle) =>
        PdfRectangle.FromCorners(rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top);

    /// <summary>Gets the union of two rectangles after normalising both.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The union.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PdfRectangle Union(in PdfRectangle first, in PdfRectangle second) => Normalize(first).Union(Normalize(second));

    /// <summary>Gets the overlap of two rectangles, all zero when they do not overlap, as PDFium's Intersect does.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns>The overlap.</returns>
    internal static PdfRectangle Intersect(in PdfRectangle first, in PdfRectangle second)
    {
        var overlap = Normalize(first).Intersect(Normalize(second));
        return overlap.Left > overlap.Right || overlap.Bottom > overlap.Top ? default : overlap;
    }

    /// <summary>Determines whether a rectangle has no area, as PDFium's IsEmpty does.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when the rectangle is empty.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsEmpty(in PdfRectangle rectangle) => rectangle.Left >= rectangle.Right || rectangle.Bottom >= rectangle.Top;

    /// <summary>Determines whether a point is inside or on the edge of a rectangle.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <param name="point">The point.</param>
    /// <returns><see langword="true"/> when the point is inside.</returns>
    internal static bool Contains(in PdfRectangle rectangle, Vector2 point)
    {
        var box = Normalize(rectangle);
        return point.X <= box.Right && point.X >= box.Left && point.Y <= box.Top && point.Y >= box.Bottom;
    }

    /// <summary>Gets the scale of one matrix axis, as PDFium's GetXUnit and GetYUnit do.</summary>
    /// <param name="along">The axis's own component.</param>
    /// <param name="across">The axis's other component.</param>
    /// <returns>The axis length.</returns>
    private static float AxisUnit(float along, float across)
    {
        if (across == 0)
        {
            return MathF.Abs(along);
        }

        return along == 0 ? MathF.Abs(across) : MathF.Sqrt((along * along) + (across * across));
    }
}
