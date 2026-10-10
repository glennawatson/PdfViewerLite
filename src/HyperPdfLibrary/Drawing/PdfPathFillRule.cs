// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>Determines which regions a path fills.</summary>
public enum PdfPathFillRule
{
    /// <summary>Fills regions according to contour winding.</summary>
    Winding = 0,
    /// <summary>Fills regions with an odd crossing count.</summary>
    EvenOdd = 1,
}
