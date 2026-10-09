// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>
/// Receives a glyph outline as it is decoded, in font units with y pointing up. Font programs call this through a
/// generic struct parameter, so the calls are devirtualised and a sink can be a ref struct.
/// </summary>
public interface IGlyphOutlineSink
{
    /// <summary>Starts a new contour.</summary>
    /// <param name="x">The x coordinate.</param>
    /// <param name="y">The y coordinate.</param>
    void MoveTo(float x, float y);

    /// <summary>Adds a straight line.</summary>
    /// <param name="x">The end x.</param>
    /// <param name="y">The end y.</param>
    void LineTo(float x, float y);

    /// <summary>Adds a quadratic curve, as TrueType outlines use.</summary>
    /// <param name="controlX">The control point x.</param>
    /// <param name="controlY">The control point y.</param>
    /// <param name="x">The end x.</param>
    /// <param name="y">The end y.</param>
    void QuadraticTo(float controlX, float controlY, float x, float y);

    /// <summary>Adds a cubic curve, as CFF and Type 1 outlines use.</summary>
    /// <param name="control1X">The first control point x.</param>
    /// <param name="control1Y">The first control point y.</param>
    /// <param name="control2X">The second control point x.</param>
    /// <param name="control2Y">The second control point y.</param>
    /// <param name="x">The end x.</param>
    /// <param name="y">The end y.</param>
    void CubicTo(float control1X, float control1Y, float control2X, float control2Y, float x, float y);

    /// <summary>Closes the current contour.</summary>
    void Close();
}
