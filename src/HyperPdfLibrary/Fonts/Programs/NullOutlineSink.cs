// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>An outline sink that ignores everything, used when only a glyph's width is wanted.</summary>
internal readonly record struct NullOutlineSink : IGlyphOutlineSink
{
    /// <inheritdoc/>
    public void MoveTo(float x, float y)
    {
    }

    /// <inheritdoc/>
    public void LineTo(float x, float y)
    {
    }

    /// <inheritdoc/>
    public void QuadraticTo(float controlX, float controlY, float x, float y)
    {
    }

    /// <inheritdoc/>
    public void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y)
    {
    }

    /// <inheritdoc/>
    public void Close()
    {
    }
}
