// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// A pattern selected as a fill or stroke colour: a shading, or one period of a tiling pattern already composed. The
/// matrix maps pattern space to the default space of the content stream that selected it.
/// </summary>
[DebuggerDisplay("PdfPatternPaint: {Shading} {Tile}")]
internal sealed class PdfPatternPaint
{
    /// <summary>Initializes a new instance of the <see cref="PdfPatternPaint"/> class for a shading pattern.</summary>
    /// <param name="shading">The shading.</param>
    /// <param name="matrix">The matrix from pattern space to the stream's default space.</param>
    internal PdfPatternPaint(PdfShading shading, Matrix3x2 matrix)
    {
        Shading = shading;
        Matrix = matrix;
    }

    /// <summary>Initializes a new instance of the <see cref="PdfPatternPaint"/> class for a tiling pattern.</summary>
    /// <param name="tile">One period of the pattern.</param>
    /// <param name="matrix">The matrix from pattern space to the stream's default space.</param>
    internal PdfPatternPaint(PatternCell tile, Matrix3x2 matrix)
    {
        Tile = tile;
        Matrix = matrix;
    }

    /// <summary>Gets the matrix from pattern space to the default space of the selecting content stream.</summary>
    internal Matrix3x2 Matrix { get; }

    /// <summary>Gets the shading, for a shading pattern.</summary>
    internal PdfShading? Shading { get; }

    /// <summary>Gets one period of the pattern, for a tiling pattern.</summary>
    internal PatternCell? Tile { get; }
}
