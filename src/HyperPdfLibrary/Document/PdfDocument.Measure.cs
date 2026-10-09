// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Viewports, measurement and geospatial data.</content>
public sealed partial class PdfDocument
{
    /// <summary>The default precision of a number format.</summary>
    private const int DefaultPrecision = 100;

    /// <summary>Gets a page's viewports (<c>/VP</c>).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The viewports.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfViewport[] GetViewports(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var list = page.Dictionary.Array("VP");
        var viewports = new List<PdfViewport>();
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is { } viewport)
            {
                viewports.Add(new(viewport.Rect("BBox"), viewport.Text("Name"), viewport.Dict("Measure") is { } measure ? ReadMeasure(measure) : null));
            }
        }

        return [.. viewports];
    }

    /// <summary>Reads a measure dictionary, from a viewport or an annotation's <c>/Measure</c>.</summary>
    /// <param name="measure">The measure dictionary.</param>
    /// <returns>The measure.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="measure"/> is null.</exception>
    public PdfMeasure ReadMeasure(PdfDictionary measure)
    {
        ArgumentNullException.ThrowIfNull(measure);
        var subtype = measure.NameText("Subtype") ?? "RL";
        return new(
            subtype,
            measure.Text("R"),
            ReadNumberFormats(measure.Array("X")),
            ReadNumberFormats(measure.Array("Y")),
            ReadNumberFormats(measure.Array("D")),
            ReadNumberFormats(measure.Array("A")),
            ReadNumberFormats(measure.Array("T")),
            ReadNumberFormats(measure.Array("S")),
            measure.Array("O").Numbers(),
            measure.Num("CYX", 1),
            subtype == "GEO" ? ReadGeospatial(measure) : null);
    }

    /// <summary>Reads an array of number format dictionaries.</summary>
    /// <param name="formats">The array, or null.</param>
    /// <returns>The formats.</returns>
    private static PdfNumberFormat[] ReadNumberFormats(PdfArray? formats)
    {
        var result = new List<PdfNumberFormat>();
        for (var i = 0; formats is not null && i < formats.Count; i++)
        {
            if (formats.GetDictionary(i) is { } format)
            {
                result.Add(ReadNumberFormat(format));
            }
        }

        return [.. result];
    }

    /// <summary>Reads one number format dictionary.</summary>
    /// <param name="format">The dictionary.</param>
    /// <returns>The format.</returns>
    private static PdfNumberFormat ReadNumberFormat(PdfDictionary format) => new(
        format.Text("U") ?? string.Empty,
        format.Num("C", 1),
        format.NameText("F") ?? "D",
        format.Int("D", DefaultPrecision),
        format.Flag("FD", false),
        format.Text("RT") ?? ",",
        format.Text("RD") ?? ".",
        format.Text("PS") ?? " ",
        format.Text("SS") ?? " ",
        format.NameText("O") ?? "S");

    /// <summary>Reads the geospatial entries of a GEO measure.</summary>
    /// <param name="measure">The measure dictionary.</param>
    /// <returns>The geospatial data.</returns>
    private static PdfGeospatial ReadGeospatial(PdfDictionary measure)
    {
        var gcs = measure.Dict("GCS");
        var epsg = gcs?.Value("EPSG") ?? default;
        return new(
            gcs?.NameText("Type"),
            gcs?.Text("WKT"),
            epsg.IsNumber ? epsg.AsInt32() : null,
            measure.Array("GPTS").Numbers(),
            measure.Array("LPTS").Numbers(),
            measure.Array("Bounds").Numbers(),
            measure.Array("PDU").NameTexts());
    }
}
