// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics.CodeAnalysis;

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>Selects standard or referred custom Huffman tables for JBIG2 segments.</summary>
internal static class Jbig2SegmentTables
{
    /// <summary>The two-bit selector value that picks a custom table.</summary>
    internal const int CustomTwoBit = 3;

    /// <summary>The one-bit selector value that picks a custom table.</summary>
    internal const int CustomOneBit = 1;

    /// <summary>The shift of the S delta selector in text region Huffman flags.</summary>
    internal const int DeltaSShift = 2;

    /// <summary>The shift of the T delta selector in text region Huffman flags.</summary>
    internal const int DeltaTShift = 4;

    /// <summary>The shift of the refinement width selector in text region Huffman flags.</summary>
    internal const int RefineWidthShift = 6;

    /// <summary>The shift of the refinement height selector in text region Huffman flags.</summary>
    internal const int RefineHeightShift = 8;

    /// <summary>The shift of the refinement X selector in text region Huffman flags.</summary>
    internal const int RefineXShift = 10;

    /// <summary>The shift of the refinement Y selector in text region Huffman flags.</summary>
    internal const int RefineYShift = 12;

    /// <summary>The shift of the refinement size selector in text region Huffman flags.</summary>
    internal const int RefineSizeShift = 14;

    /// <summary>The shift of the height delta selector in symbol dictionary flags.</summary>
    internal const int HeightDeltaShift = 2;

    /// <summary>The shift of the width delta selector in symbol dictionary flags.</summary>
    internal const int WidthDeltaShift = 4;

    /// <summary>The shift of the bitmap size selector in symbol dictionary flags.</summary>
    internal const int BitmapSizeShift = 6;

    /// <summary>The shift of the aggregate count selector in symbol dictionary flags.</summary>
    internal const int AggregateShift = 7;

    /// <summary>Gets the standard tables a first S selector picks.</summary>
    internal static ReadOnlySpan<byte> FirstSTables => [0x06, 0x07];

    /// <summary>Gets the standard tables an S delta selector picks.</summary>
    internal static ReadOnlySpan<byte> DeltaSTables => [0x08, 0x09, 0x0A];

    /// <summary>Gets the standard tables a T delta selector picks.</summary>
    internal static ReadOnlySpan<byte> DeltaTTables => [0x0B, 0x0C, 0x0D];

    /// <summary>Gets the standard tables a refinement delta selector picks.</summary>
    internal static ReadOnlySpan<byte> RefinementTables => [0x0E, 0x0F];

    /// <summary>Gets the standard table of sizes, counts and runs.</summary>
    internal static ReadOnlySpan<byte> SizeTables => [0x01];

    /// <summary>Gets the standard tables a height delta selector picks.</summary>
    internal static ReadOnlySpan<byte> HeightDeltaTables => [0x04, 0x05];

    /// <summary>Gets the standard tables a width delta selector picks.</summary>
    internal static ReadOnlySpan<byte> WidthDeltaTables => [0x02, 0x03];

    /// <summary>Selects a Huffman text region's tables (T.88 section 7.4.3.1.2).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment, whose referred table segments supply custom tables in order.</param>
    /// <param name="settings">The region parameters, which receive the tables.</param>
    /// <param name="flags">The Huffman table selection flags.</param>
    /// <returns><see langword="false"/> when a selector is reserved or a custom table is missing.</returns>
    internal static bool TrySelectTextTables(Jbig2DecodeState state, Jbig2Segment segment, Jbig2TextRegionSettings settings, int flags)
    {
        var index = 0;
        if (!TryPick(state, segment, flags & Jbig2TextSegments.TwoBits, CustomTwoBit, FirstSTables, ref index, out var firstS)
            || !TryPick(state, segment, (flags >> DeltaSShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, DeltaSTables, ref index, out var deltaS)
            || !TryPick(state, segment, (flags >> DeltaTShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, DeltaTTables, ref index, out var deltaT)
            || !TryPickRefinement(state, segment, flags, settings, ref index))
        {
            return false;
        }

        settings.FirstS = firstS;
        settings.DeltaS = deltaS;
        settings.DeltaT = deltaT;
        return true;
    }

    /// <summary>Selects a Huffman text region's refinement tables.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="flags">The Huffman table selection flags.</param>
    /// <param name="settings">The region parameters, which receive the tables.</param>
    /// <param name="index">The next custom table.</param>
    /// <returns><see langword="false"/> when a selector is reserved or a custom table is missing.</returns>
    internal static bool TryPickRefinement(Jbig2DecodeState state, Jbig2Segment segment, int flags, Jbig2TextRegionSettings settings, ref int index)
    {
        if (!TryPick(state, segment, (flags >> RefineWidthShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, RefinementTables, ref index, out var width)
            || !TryPick(state, segment, (flags >> RefineHeightShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, RefinementTables, ref index, out var height)
            || !TryPick(state, segment, (flags >> RefineXShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, RefinementTables, ref index, out var x)
            || !TryPick(state, segment, (flags >> RefineYShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, RefinementTables, ref index, out var y)
            || !TryPick(state, segment, (flags >> RefineSizeShift) & 1, CustomOneBit, SizeTables, ref index, out var size))
        {
            return false;
        }

        settings.RefineWidth = width;
        settings.RefineHeight = height;
        settings.RefineX = x;
        settings.RefineY = y;
        settings.RefineSize = size;
        return true;
    }

    /// <summary>Selects a Huffman symbol dictionary's tables (T.88 section 7.4.2.1.1).</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment, whose referred table segments supply custom tables in order.</param>
    /// <param name="settings">The dictionary parameters, which receive the tables.</param>
    /// <param name="flags">The dictionary flags.</param>
    /// <returns><see langword="false"/> when a selector is reserved or a custom table is missing.</returns>
    internal static bool TrySelectDictionaryTables(Jbig2DecodeState state, Jbig2Segment segment, Jbig2SymbolDictionarySettings settings, int flags)
    {
        if (!settings.Huffman)
        {
            return true;
        }

        var index = 0;
        if (!TryPick(state, segment, (flags >> HeightDeltaShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, HeightDeltaTables, ref index, out var height)
            || !TryPick(state, segment, (flags >> WidthDeltaShift) & Jbig2TextSegments.TwoBits, CustomTwoBit, WidthDeltaTables, ref index, out var width)
            || !TryPick(state, segment, (flags >> BitmapSizeShift) & 1, CustomOneBit, SizeTables, ref index, out var size))
        {
            return false;
        }

        settings.HeightDelta = height;
        settings.WidthDelta = width;
        settings.BitmapSize = size;
        if (!settings.RefinementAggregate)
        {
            return true;
        }

        var picked = TryPick(state, segment, (flags >> AggregateShift) & 1, CustomOneBit, SizeTables, ref index, out var aggregate);
        settings.AggregateCount = aggregate ?? settings.AggregateCount;
        return picked;
    }

    /// <summary>Picks a standard table, or the next custom table a segment refers to.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The segment.</param>
    /// <param name="selector">The selector value.</param>
    /// <param name="custom">The selector value that picks a custom table.</param>
    /// <param name="standards">The standard table numbers of the other selector values.</param>
    /// <param name="index">The next custom table, moved on when one is used.</param>
    /// <param name="table">The table.</param>
    /// <returns><see langword="false"/> when the selector is reserved or the custom table is missing.</returns>
    internal static bool TryPick(Jbig2DecodeState state, Jbig2Segment segment, int selector, int custom, ReadOnlySpan<byte> standards, ref int index, [NotNullWhen(true)] out Jbig2HuffmanTable? table)
    {
        if (selector == custom)
        {
            table = Jbig2Segments.FindReferredTable(state, segment, index);
            index++;
            return table is not null;
        }

        table = selector < standards.Length ? Jbig2HuffmanTable.GetStandard(standards[selector]) : null;
        return table is not null;
    }
}
