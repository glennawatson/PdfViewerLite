// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>The shading types of PDF 32000 §8.7.4.5.</summary>
internal enum PdfShadingKind
{
    /// <summary>Type 1: a colour function of x and y.</summary>
    FunctionBased = 1,

    /// <summary>Type 2: a gradient along an axis.</summary>
    Axial = 2,

    /// <summary>Type 3: a gradient between two circles.</summary>
    Radial = 3,

    /// <summary>Type 4: free-form Gouraud triangles.</summary>
    FreeForm = 4,

    /// <summary>Type 5: a lattice of Gouraud triangles.</summary>
    Lattice = 5,

    /// <summary>Type 6: Coons patches.</summary>
    Coons = 6,

    /// <summary>Type 7: tensor-product patches.</summary>
    Tensor = 7,
}
