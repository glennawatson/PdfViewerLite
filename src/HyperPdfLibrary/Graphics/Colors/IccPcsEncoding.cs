// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>How a look-up table stores its output in the profile connection space (PCS), as values from 0 to 1.</summary>
internal enum IccPcsEncoding
{
    /// <summary>CIE XYZ, where 1.0 stands for 1 + 32767/32768.</summary>
    Xyz = 0,

    /// <summary>CIE Lab as v4 profiles and 8-bit tables store it: L* is the value times 100; a* and b* are the value times 255 minus 128.</summary>
    Lab = 1,

    /// <summary>CIE Lab as 16-bit tables store it: 0xFF00 is L* of 100 and 0x8000 is a* or b* of zero.</summary>
    LabLegacy16 = 2,
}
