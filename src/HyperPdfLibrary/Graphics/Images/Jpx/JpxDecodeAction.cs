// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>How the decoded channels become PDF samples, after PDFium's <c>JpxDecodeConversion</c>.</summary>
internal enum JpxDecodeAction
{
    /// <summary>Keep the image dictionary's colour space and component count.</summary>
    DoNothing = 0,

    /// <summary>One gray channel.</summary>
    UseGray = 1,

    /// <summary>Raw palette indices for an Indexed colour space.</summary>
    UseIndexed = 2,

    /// <summary>Three RGB channels.</summary>
    UseRgb = 3,

    /// <summary>Four CMYK channels.</summary>
    UseCmyk = 4,

    /// <summary>Three RGB channels from four, the fourth being alpha.</summary>
    ConvertArgbToRgb = 5,
}
