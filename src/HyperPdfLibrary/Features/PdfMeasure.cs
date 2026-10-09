// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A measure dictionary: rectilinear (RL) or geospatial (GEO).</summary>
/// <param name="Subtype">RL or GEO.</param>
/// <param name="Ratio">The scale ratio text (<c>/R</c>), for example "1 in = 1 mi"; null for GEO.</param>
/// <param name="X">How to show x distances (<c>/X</c>).</param>
/// <param name="Y">How to show y distances (<c>/Y</c>); empty means the same as X.</param>
/// <param name="Distance">How to show distances (<c>/D</c>).</param>
/// <param name="Area">How to show areas (<c>/A</c>).</param>
/// <param name="Angle">How to show angles (<c>/T</c>).</param>
/// <param name="Slope">How to show slopes (<c>/S</c>).</param>
/// <param name="Origin">The origin of the measurement coordinates (<c>/O</c>); empty means 0 0.</param>
/// <param name="YToXRatio">The ratio of y to x units (<c>/CYX</c>); 1 when missing.</param>
/// <param name="Geo">The geospatial data for GEO; null for RL.</param>
[DebuggerDisplay("PdfMeasure: {Subtype} {Ratio}")]
public sealed record PdfMeasure(
    string Subtype,
    string? Ratio,
    PdfNumberFormat[] X,
    PdfNumberFormat[] Y,
    PdfNumberFormat[] Distance,
    PdfNumberFormat[] Area,
    PdfNumberFormat[] Angle,
    PdfNumberFormat[] Slope,
    double[] Origin,
    double YToXRatio,
    PdfGeospatial? Geo);
