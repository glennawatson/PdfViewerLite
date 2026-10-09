// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>Geospatial data of a viewport (a <c>/GEO</c> measure dictionary).</summary>
/// <param name="CoordinateSystemType">The geographic coordinate system type: PROJCS or GEOGCS; null when missing.</param>
/// <param name="WellKnownText">The coordinate system as well-known text (<c>/GCS /WKT</c>), or null.</param>
/// <param name="Epsg">The EPSG code (<c>/GCS /EPSG</c>), or null.</param>
/// <param name="GeoPoints">The latitude and longitude pairs (<c>/GPTS</c>).</param>
/// <param name="LocalPoints">The matching points in the viewport's unit square (<c>/LPTS</c>).</param>
/// <param name="Bounds">The bounds of the viewport in the unit square (<c>/Bounds</c>); empty when missing.</param>
/// <param name="DisplayUnits">The preferred display units (<c>/PDU</c>).</param>
[DebuggerDisplay("PdfGeospatial: {CoordinateSystemType} EPSG {Epsg}")]
public sealed record PdfGeospatial(string? CoordinateSystemType, string? WellKnownText, int? Epsg, double[] GeoPoints, double[] LocalPoints, double[] Bounds, string[] DisplayUnits);
