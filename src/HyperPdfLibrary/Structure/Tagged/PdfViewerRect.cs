// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A rectangle in viewer space: points from the page's top-left corner, with the page's rotation applied.</summary>
/// <param name="Left">The left edge.</param>
/// <param name="Top">The top edge.</param>
/// <param name="Right">The right edge.</param>
/// <param name="Bottom">The bottom edge, below the top.</param>
[DebuggerDisplay("PdfViewerRect: {Left},{Top} to {Right},{Bottom}")]
public readonly record struct PdfViewerRect(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Half, for centres.</summary>
    private const float Half = 0.5F;

    /// <summary>Gets the width.</summary>
    public float Width => Right - Left;

    /// <summary>Gets the height.</summary>
    public float Height => Bottom - Top;

    /// <summary>Gets a value indicating whether the rectangle has no area.</summary>
    public bool IsEmpty => !(Width > 0 && Height > 0);

    /// <summary>Gets the horizontal centre.</summary>
    public float CenterX => (Left + Right) * Half;

    /// <summary>Gets the vertical centre.</summary>
    public float CenterY => (Top + Bottom) * Half;

    /// <summary>Converts a rectangle that <see cref="Document.PdfPage.ToViewerRectangle"/> returned, whose bottom is its smaller y.</summary>
    /// <param name="rectangle">The rectangle in viewer space.</param>
    /// <returns>The rectangle.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfViewerRect FromViewerRectangle(PdfRectangle rectangle) => new(rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top);

    /// <summary>Gets the bounding box of a rectangle after a transform.</summary>
    /// <param name="left">The rectangle's left edge before the transform.</param>
    /// <param name="bottom">The rectangle's lower edge before the transform.</param>
    /// <param name="right">The rectangle's right edge before the transform.</param>
    /// <param name="top">The rectangle's upper edge before the transform.</param>
    /// <param name="matrix">The transform.</param>
    /// <returns>The bounding box.</returns>
    public static PdfViewerRect Transform(float left, float bottom, float right, float top, Matrix3x2 matrix)
    {
        var a = Vector2.Transform(new(left, bottom), matrix);
        var b = Vector2.Transform(new(right, bottom), matrix);
        var c = Vector2.Transform(new(right, top), matrix);
        var d = Vector2.Transform(new(left, top), matrix);
        var min = Vector2.Min(Vector2.Min(a, b), Vector2.Min(c, d));
        var max = Vector2.Max(Vector2.Max(a, b), Vector2.Max(c, d));
        return new(min.X, min.Y, max.X, max.Y);
    }

    /// <summary>Gets the smallest rectangle covering this one and another; an empty one adds nothing.</summary>
    /// <param name="other">The other rectangle.</param>
    /// <returns>The union.</returns>
    public PdfViewerRect Union(PdfViewerRect other)
    {
        if (other.IsEmpty)
        {
            return this;
        }

        return IsEmpty
            ? other
            : new(Math.Min(Left, other.Left), Math.Min(Top, other.Top), Math.Max(Right, other.Right), Math.Max(Bottom, other.Bottom));
    }
}
