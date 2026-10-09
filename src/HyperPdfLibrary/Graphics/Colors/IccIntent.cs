// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>The ICC rendering intents, which are also the PDF /RI names.</summary>
internal enum IccIntent
{
    /// <summary>Perceptual: uses the profile's AToB0 table.</summary>
    Perceptual = 0,

    /// <summary>Relative colorimetric: uses the profile's AToB1 table.</summary>
    RelativeColorimetric = 1,

    /// <summary>Saturation: uses the profile's AToB2 table.</summary>
    Saturation = 2,

    /// <summary>Absolute colorimetric: uses the profile's AToB1 table; the media white point is not simulated.</summary>
    AbsoluteColorimetric = 3,
}
