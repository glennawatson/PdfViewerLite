// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Runs the high-throughput magnitude refinement and significance propagation passes.</summary>
internal static class JpxHtRefinement
{
    /// <summary>The most samples that can become significant in one group of columns.</summary>
    internal const int GroupSamples = JpxBlockLayout.StripeRows * JpxBlockLayout.StripeRows;

    /// <summary>The value, at twice scale relative to the refinement plane, of a sample made significant by SigProp: the bit and its mid-point.</summary>
    internal const int SigPropValue = 3;

    /// <summary>Runs the MagRef pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    internal static void HtMagRef(JpxBlockState state, ReadOnlySpan<byte> segment, int planes)
    {
        var reader = JpxHtReverseReader.ForMagRef(segment);
        var clear = 1 << (planes - 1);
        var half = 1 << (planes - JpxBlockLayout.QuadWidth);
        for (var stripe = 0; stripe < state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, state.Height - (stripe * JpxBlockLayout.StripeRows));
            var index = ((stripe + 1) * state.StripeStride) + JpxBlockLayout.StripeRows;
            for (var x = 0; x < state.Width; x++)
            {
                if ((JpxBlockLayout.ReadColumn(state.Flags, index) & JpxBlockLayout.ColumnSignificant) != 0)
                {
                    MagRefColumn(state, ref reader, index, rows, clear, half);
                }

                index += JpxBlockLayout.StripeRows;
            }
        }
    }

    /// <summary>Refines the significant samples of one stripe column.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The MagRef stream.</param>
    /// <param name="index">The column's first sample.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="clear">The cleanup mid-point bit, cleared when the refinement bit is zero.</param>
    /// <param name="half">The new mid-point bit.</param>
    internal static void MagRefColumn(JpxBlockState state, ref JpxHtReverseReader reader, int index, int rows, int clear, int half)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            if ((state.Flags[i] & JpxBlockLayout.Significant) == 0)
            {
                continue;
            }

            var bit = (int)(reader.Peek() & 1);
            reader.Skip(1);
            state.Values[i] = (state.Values[i] ^ ((1 - bit) * clear)) | half;
        }
    }

    /// <summary>Runs the SigProp pass.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    internal static void HtSigProp(JpxBlockState state, ReadOnlySpan<byte> segment, int planes)
    {
        var reader = new JpxHtForwardReader(segment, 0);
        var value = SigPropValue << (planes - JpxBlockLayout.QuadWidth);
        Span<int> found = stackalloc int[GroupSamples];
        for (var stripe = 0; stripe < state.Stripes; stripe++)
        {
            var rows = Math.Min(JpxBlockLayout.StripeRows, state.Height - (stripe * JpxBlockLayout.StripeRows));
            for (var x = 0; x < state.Width; x += JpxBlockLayout.StripeRows)
            {
                var count = SigPropGroup(state, ref reader, JpxBlockLayout.HtIndex(state, x, stripe * JpxBlockLayout.StripeRows), Math.Min(JpxBlockLayout.StripeRows, state.Width - x), rows, found);
                for (var k = 0; k < count; k++)
                {
                    state.Values[found[k]] = (int)((reader.ReadBit() << JpxBlockLayout.SignShift) | (uint)value);
                }
            }
        }
    }

    /// <summary>Reads the significance bits of a group of up to four columns of a stripe.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="reader">The SigProp stream.</param>
    /// <param name="index">The group's first sample.</param>
    /// <param name="columns">The columns in the group.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="found">Receives the samples that became significant, in scan order.</param>
    /// <returns>The number of samples that became significant.</returns>
    internal static int SigPropGroup(JpxBlockState state, ref JpxHtForwardReader reader, int index, int columns, int rows, scoped Span<int> found)
    {
        var count = 0;
        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                var i = index + (column * JpxBlockLayout.StripeRows) + row;
                if (state.Flags[i] != 0 || !HasSignificantNeighbour(state, i, row) || reader.ReadBit() == 0)
                {
                    continue;
                }

                state.Flags[i] = JpxBlockLayout.NewlySignificant;
                found[count] = i;
                count++;
            }
        }

        return count;
    }

    /// <summary>
    /// Determines whether any of a sample's eight neighbours is significant: from the cleanup pass, or from this pass
    /// earlier in scan order. In the vertically causal mode the stripe below is not looked at.
    /// </summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="index">The sample.</param>
    /// <param name="row">The sample's row within its stripe.</param>
    /// <returns><see langword="true"/> when the sample is coded in this pass.</returns>
    internal static bool HasSignificantNeighbour(JpxBlockState state, int index, int row)
    {
        var up = row == 0 ? index - state.StripeStride + JpxBlockLayout.LastRow : index - 1;
        var flags = state.Flags[up - JpxBlockLayout.StripeRows] | state.Flags[up]
            | state.Flags[up + JpxBlockLayout.StripeRows] | state.Flags[index - JpxBlockLayout.StripeRows]
            | state.Flags[index + JpxBlockLayout.StripeRows];
        if (row == JpxBlockLayout.LastRow && state.Causal)
        {
            return flags != 0;
        }

        var down = row == JpxBlockLayout.LastRow ? index + state.StripeStride - JpxBlockLayout.LastRow : index + 1;
        return (flags | state.Flags[down - JpxBlockLayout.StripeRows] | state.Flags[down] | state.Flags[down + JpxBlockLayout.StripeRows]) != 0;
    }
}
