// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The tile-wide coding parameters of a COD marker: the Scod flags and the SGcod fields.</summary>
/// <param name="Order">The progression order.</param>
/// <param name="Layers">The number of quality layers.</param>
/// <param name="ComponentTransform">Whether the first three components use a multiple component transform.</param>
/// <param name="StartOfPacketMarkers">Whether packets may start with SOP markers.</param>
/// <param name="EndOfHeaderMarkers">Whether packet headers end with EPH markers.</param>
[DebuggerDisplay("JpxTileCoding: {Order}, {Layers} layers")]
internal sealed record JpxTileCoding(JpxProgressionOrder Order, int Layers, bool ComponentTransform, bool StartOfPacketMarkers, bool EndOfHeaderMarkers)
{
    /// <summary>The bytes of Scod and SGcod.</summary>
    internal const int Bytes = 5;

    /// <summary>The Scod bit for SOP markers.</summary>
    private const int SopBit = 2;

    /// <summary>The Scod bit for EPH markers.</summary>
    private const int EphBit = 4;

    /// <summary>The offset of the layer count.</summary>
    private const int LayersOffset = 2;

    /// <summary>The offset of the transform flag.</summary>
    private const int TransformOffset = 4;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>Reads Scod and SGcod.</summary>
    /// <param name="fields">The COD segment after its length.</param>
    /// <returns>The parameters, or <see langword="null"/> when invalid.</returns>
    internal static JpxTileCoding? Read(ReadOnlySpan<byte> fields)
    {
        if (fields.Length < Bytes)
        {
            return null;
        }

        var order = fields[1];
        var layers = (fields[LayersOffset] << ByteBits) | fields[LayersOffset + 1];
        if (order > (int)JpxProgressionOrder.ComponentPositionResolutionLayer || layers == 0)
        {
            return null;
        }

        var scod = fields[0];
        return new((JpxProgressionOrder)order, layers, fields[TransformOffset] != 0, (scod & SopBit) != 0, (scod & EphBit) != 0);
    }
}
