// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Rectangle tests for deciding which page objects an area touches.</summary>
internal static class PageGeometry
{
    /// <summary>Half, for the centre of a rectangle and half of an area.</summary>
    private const float Half = 0.5F;

    /// <summary>Determines whether two rectangles share any area.</summary>
    /// <param name="first">The first rectangle.</param>
    /// <param name="second">The second rectangle.</param>
    /// <returns><see langword="true"/> when they overlap.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Overlaps(in PdfRectangle first, in PdfRectangle second) =>
        first.Left < second.Right && second.Left < first.Right && first.Bottom < second.Top && second.Bottom < first.Top;

    /// <summary>Determines whether one rectangle lies wholly inside another.</summary>
    /// <param name="outer">The outer rectangle.</param>
    /// <param name="inner">The inner rectangle.</param>
    /// <returns><see langword="true"/> when the inner rectangle is inside.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Contains(in PdfRectangle outer, in PdfRectangle inner) =>
        inner.Left >= outer.Left && inner.Right <= outer.Right && inner.Bottom >= outer.Bottom && inner.Top <= outer.Top;

    /// <summary>
    /// Determines whether an area removes a glyph: the glyph box's centre is inside the area, or the area covers at least
    /// half of the box. Boxes with no area (spaces in a zero-width font) count when the area holds their centre.
    /// </summary>
    /// <param name="area">The area.</param>
    /// <param name="box">The glyph's box.</param>
    /// <returns><see langword="true"/> when the glyph is removed.</returns>
    internal static bool Covers(in PdfRectangle area, in PdfRectangle box)
    {
        var centreX = (box.Left + box.Right) * Half;
        var centreY = (box.Bottom + box.Top) * Half;
        if (centreX >= area.Left && centreX <= area.Right && centreY >= area.Bottom && centreY <= area.Top)
        {
            return true;
        }

        var boxArea = box.Width * box.Height;
        if (!(boxArea > 0))
        {
            return false;
        }

        var overlap = area.Intersect(box);
        return !overlap.IsEmpty && overlap.Width * overlap.Height >= boxArea * Half;
    }

    /// <summary>Determines whether a rectangle has a usable, finite size.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when all four edges are finite.</returns>
    internal static bool IsFinite(in PdfRectangle rectangle) =>
        float.IsFinite(rectangle.Left) && float.IsFinite(rectangle.Bottom) && float.IsFinite(rectangle.Right) && float.IsFinite(rectangle.Top);
}
