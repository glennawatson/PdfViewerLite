// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Provides geometry shared by generated annotation appearances.</summary>
internal static class AnnotationAppearanceGeometry
{
    /// <summary>Half, to centre a line on an edge.</summary>
    internal const float Half = 0.5F;

    /// <summary>Gets an annotation's normalised /Rect.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The rectangle, or an empty one.</returns>
    internal static PdfRectangle Rect(PdfDictionary annotation) =>
        annotation.TryGetRectangle(KnownName.Rect, out var rect) ? PdfRectangle.FromCorners(rect.Left, rect.Bottom, rect.Right, rect.Top) : default;

    /// <summary>Shrinks a rectangle on every side.</summary>
    /// <param name="rect">The rectangle.</param>
    /// <param name="amount">How far to move each side in.</param>
    /// <returns>The smaller rectangle.</returns>
    internal static PdfRectangle Deflate(PdfRectangle rect, float amount) =>
        new(rect.Left + amount, rect.Bottom + amount, rect.Right - amount, rect.Top - amount);
}
