// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics;

/// <summary>The PDF blend modes (PDF 32000 §11.3.5).</summary>
public enum PdfBlendMode
{
    /// <summary>Normal (and Compatible).</summary>
    Normal = 0,

    /// <summary>Multiply blending.</summary>
    Multiply = 1,

    /// <summary>Screen blending.</summary>
    Screen = 2,

    /// <summary>Overlay blending.</summary>
    Overlay = 3,

    /// <summary>Darken blending.</summary>
    Darken = 4,

    /// <summary>Lighten blending.</summary>
    Lighten = 5,

    /// <summary>Colour dodge.</summary>
    ColorDodge = 6,

    /// <summary>Colour burn.</summary>
    ColorBurn = 7,

    /// <summary>Hard light.</summary>
    HardLight = 8,

    /// <summary>Soft light.</summary>
    SoftLight = 9,

    /// <summary>Difference blending.</summary>
    Difference = 10,

    /// <summary>Exclusion blending.</summary>
    Exclusion = 11,

    /// <summary>Hue blending.</summary>
    Hue = 12,

    /// <summary>Saturation blending.</summary>
    Saturation = 13,

    /// <summary>Colour blending.</summary>
    Color = 14,

    /// <summary>Luminosity blending.</summary>
    Luminosity = 15,
}
