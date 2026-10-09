// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One sub-band of a resolution level.</summary>
/// <param name="Area">The sub-band's area in its own sample grid.</param>
/// <param name="Orientation">0 for LL, 1 for HL (horizontally high-pass), 2 for LH and 3 for HH.</param>
/// <param name="Component">The component index.</param>
/// <param name="BufferX">The column of the sub-band's first sample in the tile-component buffer.</param>
/// <param name="BufferY">The row of the sub-band's first sample in the tile-component buffer.</param>
/// <param name="BlockWidthExponent">The code-block width as a power of two, limited by the precinct.</param>
/// <param name="BlockHeightExponent">The code-block height as a power of two, limited by the precinct.</param>
/// <param name="Magnitude">The magnitude bit-planes Mb of the sub-band (equation E-2).</param>
/// <param name="StepSize">Half the dequantization step: what the decoded integers multiply by.</param>
/// <param name="FirstPrecinct">The index of the sub-band's first precinct.</param>
internal readonly record struct JpxBandLayout(
    JpxRectangle Area,
    int Orientation,
    int Component,
    int BufferX,
    int BufferY,
    int BlockWidthExponent,
    int BlockHeightExponent,
    int Magnitude,
    float StepSize,
    int FirstPrecinct);
