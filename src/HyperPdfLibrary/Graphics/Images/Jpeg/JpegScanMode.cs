// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>What a JPEG scan carries.</summary>
internal enum JpegScanMode
{
    /// <summary>Complete blocks, DC and AC together.</summary>
    Sequential = 0,

    /// <summary>The first pass over the DC coefficients of a progressive image.</summary>
    DcFirst = 1,

    /// <summary>A refinement pass over the DC coefficients.</summary>
    DcRefine = 2,

    /// <summary>The first pass over a band of AC coefficients.</summary>
    AcFirst = 3,

    /// <summary>A refinement pass over a band of AC coefficients.</summary>
    AcRefine = 4,
}
