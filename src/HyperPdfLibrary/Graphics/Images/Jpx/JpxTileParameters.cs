// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The coding parameters in force for one tile, resolved by the precedence of ISO 15444-1 A.6: a tile-part COC beats a
/// tile-part COD, which beats a main COC, which beats the main COD. QCC, QCD and RGN follow the same order.
/// </summary>
[DebuggerDisplay("JpxTileParameters: {Coding}")]
internal sealed class JpxTileParameters
{
    /// <summary>Initializes a new instance of the <see cref="JpxTileParameters"/> class.</summary>
    /// <param name="coding">The tile-wide coding parameters.</param>
    /// <param name="styles">The coding style of each component.</param>
    /// <param name="quantizations">The quantization of each component.</param>
    /// <param name="roiShifts">The region-of-interest shift of each component.</param>
    /// <param name="changes">The progression changes, or <see langword="null"/>.</param>
    private JpxTileParameters(JpxTileCoding coding, JpxCodingStyle[] styles, JpxQuantization[] quantizations, int[] roiShifts, List<JpxProgressionChange>? changes)
    {
        Coding = coding;
        Styles = styles;
        Quantizations = quantizations;
        RoiShifts = roiShifts;
        Changes = changes;
    }

    /// <summary>Gets the tile-wide coding parameters.</summary>
    internal JpxTileCoding Coding { get; }

    /// <summary>Gets the coding style of each component.</summary>
    internal JpxCodingStyle[] Styles { get; }

    /// <summary>Gets the quantization of each component.</summary>
    internal JpxQuantization[] Quantizations { get; }

    /// <summary>Gets the region-of-interest shift of each component.</summary>
    internal int[] RoiShifts { get; }

    /// <summary>Gets the progression changes, or <see langword="null"/> to use the coding order.</summary>
    internal List<JpxProgressionChange>? Changes { get; }

    /// <summary>Resolves the parameters of a tile.</summary>
    /// <param name="main">The main header parameters, which set COD and QCD.</param>
    /// <param name="tile">The tile's own header parameters.</param>
    /// <returns>The resolved parameters.</returns>
    internal static JpxTileParameters Resolve(JpxHeaderState main, JpxHeaderState tile)
    {
        var count = main.ComponentStyles.Length;
        var styles = new JpxCodingStyle[count];
        var quantizations = new JpxQuantization[count];
        var shifts = new int[count];
        for (var i = 0; i < count; i++)
        {
            styles[i] = StyleOf(main, tile, i);
            quantizations[i] = QuantizationOf(main, tile, i);
            shifts[i] = Math.Max(tile.RoiShifts[i] >= 0 ? tile.RoiShifts[i] : main.RoiShifts[i], 0);
        }

        return new(tile.Coding ?? main.Coding!, styles, quantizations, shifts, tile.Changes ?? main.Changes);
    }

    /// <summary>Gets a component's coding style by marker precedence.</summary>
    /// <param name="main">The main header parameters.</param>
    /// <param name="tile">The tile's header parameters.</param>
    /// <param name="component">The component index.</param>
    /// <returns>The coding style.</returns>
    private static JpxCodingStyle StyleOf(JpxHeaderState main, JpxHeaderState tile, int component) =>
        tile.ComponentStyles[component] ?? tile.Style ?? main.ComponentStyles[component] ?? main.Style!;

    /// <summary>Gets a component's quantization by marker precedence.</summary>
    /// <param name="main">The main header parameters.</param>
    /// <param name="tile">The tile's header parameters.</param>
    /// <param name="component">The component index.</param>
    /// <returns>The quantization.</returns>
    private static JpxQuantization QuantizationOf(JpxHeaderState main, JpxHeaderState tile, int component) =>
        tile.ComponentQuantizations[component] ?? tile.Quantization ?? main.ComponentQuantizations[component] ?? main.Quantization!;
}
