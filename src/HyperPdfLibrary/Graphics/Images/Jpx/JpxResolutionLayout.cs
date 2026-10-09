// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One resolution level of a tile-component.</summary>
/// <param name="Area">The resolution's area in its own sample grid.</param>
/// <param name="PrecinctWidthExponent">The precinct width as a power of two.</param>
/// <param name="PrecinctHeightExponent">The precinct height as a power of two.</param>
/// <param name="PrecinctsWide">The precincts across.</param>
/// <param name="PrecinctsHigh">The precincts down.</param>
/// <param name="FirstBand">The index of the first sub-band.</param>
/// <param name="BandCount">The number of sub-bands: one for the lowest resolution, otherwise three.</param>
internal readonly record struct JpxResolutionLayout(
    JpxRectangle Area,
    int PrecinctWidthExponent,
    int PrecinctHeightExponent,
    int PrecinctsWide,
    int PrecinctsHigh,
    int FirstBand,
    int BandCount)
{
    /// <summary>Gets the number of precincts.</summary>
    internal int PrecinctCount => PrecinctsWide * PrecinctsHigh;
}
