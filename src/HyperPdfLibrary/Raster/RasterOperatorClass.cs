// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Raster;

/// <summary>What a content operator means to the raster-only check.</summary>
internal enum RasterOperatorClass
{
    /// <summary>The operator sets state or builds a path and paints nothing.</summary>
    Neutral = 0,

    /// <summary>The <c>q</c> operator, which saves the graphics state.</summary>
    Save = 1,

    /// <summary>The <c>Q</c> operator, which restores the graphics state.</summary>
    Restore = 2,

    /// <summary>The <c>cm</c> operator, which concatenates a matrix.</summary>
    Matrix = 3,

    /// <summary>The <c>Do</c> operator, which paints an XObject.</summary>
    PaintXObject = 4,

    /// <summary>The <c>BI</c> operator, which begins an inline image.</summary>
    InlineImage = 5,

    /// <summary>An operator that fills, strokes or shades.</summary>
    Vector = 6,

    /// <summary>An operator that shows text.</summary>
    ShowText = 7,

    /// <summary>The <c>Tr</c> operator, which sets the text render mode.</summary>
    RenderMode = 8,
}
