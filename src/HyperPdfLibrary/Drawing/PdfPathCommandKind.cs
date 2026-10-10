// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>The drawing operation represented by a path command.</summary>
public enum PdfPathCommandKind
{
    /// <summary>Starts a contour at a point.</summary>
    MoveTo = 0,
    /// <summary>Draws a line to a point.</summary>
    LineTo = 1,
    /// <summary>Draws a quadratic curve to a point.</summary>
    QuadraticTo = 2,
    /// <summary>Draws a cubic curve to a point.</summary>
    CubicTo = 3,
    /// <summary>Closes the current contour.</summary>
    Close = 4,
}
