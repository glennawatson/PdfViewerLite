// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The coding parameters one header sets: the main header, or the tile-part headers of one tile. Values left
/// <see langword="null"/> fall back to the main header; component-specific markers win over the defaults of the same header.
/// </summary>
[DebuggerDisplay("JpxHeaderState: coding {Coding}")]
internal sealed class JpxHeaderState
{
    /// <summary>The bytes of one progression change with a one-byte component index.</summary>
    private const int ChangeBytes = 7;

    /// <summary>The offset of the layer end in a progression change.</summary>
    private const int LayerEndOffset = 2;

    /// <summary>The offset of the resolution end in a progression change.</summary>
    private const int ResolutionEndOffset = 4;

    /// <summary>The offset of the component end in a progression change.</summary>
    private const int ComponentEndOffset = 5;

    /// <summary>The offset of the order in a progression change.</summary>
    private const int OrderOffset = 6;

    /// <summary>The component end value that means 256 components.</summary>
    private const int AllComponents = 256;

    /// <summary>The bytes of an RGN segment with a one-byte component index.</summary>
    private const int RoiBytes = 3;

    /// <summary>The offset of the shift in an RGN segment.</summary>
    private const int RoiShiftOffset = 2;

    /// <summary>The marker value that means no region-of-interest shift was given.</summary>
    private const int Unset = -1;

    /// <summary>Initializes a new instance of the <see cref="JpxHeaderState"/> class.</summary>
    /// <param name="components">The number of components.</param>
    internal JpxHeaderState(int components)
    {
        ComponentStyles = new JpxCodingStyle?[components];
        ComponentQuantizations = new JpxQuantization?[components];
        RoiShifts = new int[components];
        RoiShifts.AsSpan().Fill(Unset);
    }

    /// <summary>Gets the per-component coding styles from COC markers.</summary>
    internal JpxCodingStyle?[] ComponentStyles { get; }

    /// <summary>Gets the per-component quantizations from QCC markers.</summary>
    internal JpxQuantization?[] ComponentQuantizations { get; }

    /// <summary>Gets the per-component region-of-interest shifts from RGN markers, or -1.</summary>
    internal int[] RoiShifts { get; }

    /// <summary>Gets the tile-wide coding parameters from COD, or <see langword="null"/>.</summary>
    internal JpxTileCoding? Coding { get; private set; }

    /// <summary>Gets the default coding style from COD, or <see langword="null"/>.</summary>
    internal JpxCodingStyle? Style { get; private set; }

    /// <summary>Gets the default quantization from QCD, or <see langword="null"/>.</summary>
    internal JpxQuantization? Quantization { get; private set; }

    /// <summary>Gets the progression changes from POC markers, or <see langword="null"/>.</summary>
    internal List<JpxProgressionChange>? Changes { get; private set; }

    /// <summary>Gets the packed packet header pieces from PPM or PPT markers, offsets into the codestream, or <see langword="null"/>.</summary>
    internal List<JpxDataRange>? PackedHeaders { get; private set; }

    /// <summary>Reads one marker segment; markers the decoder does not need are skipped.</summary>
    /// <param name="marker">The marker code.</param>
    /// <param name="segment">The segment after its length field.</param>
    /// <param name="offset">The offset of <paramref name="segment"/> in the codestream.</param>
    /// <returns><see langword="false"/> when a needed segment is invalid.</returns>
    internal bool Read(int marker, ReadOnlySpan<byte> segment, int offset) => marker switch
    {
        JpxMarkers.CodingStyle => ReadCodingStyle(segment),
        JpxMarkers.ComponentCodingStyle => ReadComponentStyle(segment),
        JpxMarkers.Quantization => (Quantization = JpxQuantization.Read(segment)) is not null,
        JpxMarkers.ComponentQuantization => ReadComponentQuantization(segment),
        JpxMarkers.RegionOfInterest => ReadRegion(segment),
        JpxMarkers.ProgressionChange => ReadChanges(segment),
        JpxMarkers.PackedMainHeaders or JpxMarkers.PackedTileHeaders => AddPacked(segment, offset),
        _ => true,
    };

    /// <summary>Reads a COD segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadCodingStyle(ReadOnlySpan<byte> segment)
    {
        Coding = JpxTileCoding.Read(segment);
        if (Coding is null)
        {
            return false;
        }

        Style = JpxCodingStyle.Read(segment[JpxTileCoding.Bytes..], (segment[0] & JpxCodingStyle.PrecinctsBit) != 0, out _);
        return Style is not null;
    }

    /// <summary>Reads a COC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadComponentStyle(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < JpxMarkers.MarkerBytes || segment[0] >= ComponentStyles.Length)
        {
            return false;
        }

        var style = JpxCodingStyle.Read(segment[JpxMarkers.MarkerBytes..], (segment[1] & JpxCodingStyle.PrecinctsBit) != 0, out _);
        ComponentStyles[segment[0]] = style;
        return style is not null;
    }

    /// <summary>Reads a QCC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadComponentQuantization(ReadOnlySpan<byte> segment)
    {
        if (segment.IsEmpty || segment[0] >= ComponentQuantizations.Length)
        {
            return false;
        }

        var quantization = JpxQuantization.Read(segment[1..]);
        ComponentQuantizations[segment[0]] = quantization;
        return quantization is not null;
    }

    /// <summary>Reads an RGN segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadRegion(ReadOnlySpan<byte> segment)
    {
        if (segment.Length < RoiBytes || segment[0] >= RoiShifts.Length)
        {
            return false;
        }

        RoiShifts[segment[0]] = segment[RoiShiftOffset];
        return true;
    }

    /// <summary>Reads a POC segment.</summary>
    /// <param name="segment">The segment.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool ReadChanges(ReadOnlySpan<byte> segment)
    {
        var count = segment.Length / ChangeBytes;
        if (count == 0)
        {
            return false;
        }

        // A tile's first POC replaces any inherited list; later POC segments of the same header add to it.
        Changes ??= [];
        for (var i = 0; i < count; i++)
        {
            var entry = segment.Slice(i * ChangeBytes, ChangeBytes);
            var order = entry[OrderOffset];
            if (order > (int)JpxProgressionOrder.ComponentPositionResolutionLayer)
            {
                return false;
            }

            var componentEnd = entry[ComponentEndOffset] == 0 ? AllComponents : entry[ComponentEndOffset];
            Changes.Add(new(entry[0], entry[1], BinaryPrimitives.ReadUInt16BigEndian(entry[LayerEndOffset..]), entry[ResolutionEndOffset], componentEnd, (JpxProgressionOrder)order));
        }

        return true;
    }

    /// <summary>Records a PPM or PPT segment's packed headers, after its index byte.</summary>
    /// <param name="segment">The segment.</param>
    /// <param name="offset">The offset of the segment in the codestream.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private bool AddPacked(ReadOnlySpan<byte> segment, int offset)
    {
        if (segment.IsEmpty)
        {
            return false;
        }

        (PackedHeaders ??= []).Add(new(offset + 1, segment.Length - 1));
        return true;
    }
}
