// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <content>
/// The HT refinement passes (T.814), both one bit-plane below the cleanup pass. MagRef reads one bit
/// for each sample the cleanup pass made significant, backward from the end of the refinement segment. SigProp reads,
/// forward from its start, a significance bit for each insignificant sample next to a significant one, stripe by stripe
/// and four columns at a time, then a sign for each sample that became significant in those columns.
/// </content>
internal sealed partial class JpxBlockDecoder
{
    /// <summary>The most samples that can become significant in one group of columns.</summary>
    private const int GroupSamples = StripeRows * StripeRows;

    /// <summary>The value, at twice scale relative to the refinement plane, of a sample made significant by SigProp: the bit and its mid-point.</summary>
    private const int SigPropValue = 3;

    /// <summary>Runs the MagRef pass.</summary>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    private void HtMagRef(ReadOnlySpan<byte> segment, int planes)
    {
        var reader = JpxHtReverseReader.ForMagRef(segment);
        var clear = 1 << (planes - 1);
        var half = 1 << (planes - QuadWidth);
        for (var stripe = 0; stripe < _stripes; stripe++)
        {
            var rows = Math.Min(StripeRows, _height - (stripe * StripeRows));
            var index = ((stripe + 1) * _stripeStride) + StripeRows;
            for (var x = 0; x < _width; x++)
            {
                if ((ReadColumn(_flags, index) & ColumnSignificant) != 0)
                {
                    MagRefColumn(ref reader, index, rows, clear, half);
                }

                index += StripeRows;
            }
        }
    }

    /// <summary>Refines the significant samples of one stripe column.</summary>
    /// <param name="reader">The MagRef stream.</param>
    /// <param name="index">The column's first sample.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="clear">The cleanup mid-point bit, cleared when the refinement bit is zero.</param>
    /// <param name="half">The new mid-point bit.</param>
    private void MagRefColumn(ref JpxHtReverseReader reader, int index, int rows, int clear, int half)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            if ((_flags[i] & Significant) == 0)
            {
                continue;
            }

            var bit = (int)(reader.Peek() & 1);
            reader.Skip(1);
            _values[i] = (_values[i] ^ ((1 - bit) * clear)) | half;
        }
    }

    /// <summary>Runs the SigProp pass.</summary>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    private void HtSigProp(ReadOnlySpan<byte> segment, int planes)
    {
        var reader = new JpxHtForwardReader(segment, 0);
        var value = SigPropValue << (planes - QuadWidth);
        Span<int> found = stackalloc int[GroupSamples];
        for (var stripe = 0; stripe < _stripes; stripe++)
        {
            var rows = Math.Min(StripeRows, _height - (stripe * StripeRows));
            for (var x = 0; x < _width; x += StripeRows)
            {
                var count = SigPropGroup(ref reader, HtIndex(x, stripe * StripeRows), Math.Min(StripeRows, _width - x), rows, found);
                for (var k = 0; k < count; k++)
                {
                    _values[found[k]] = (int)((reader.ReadBit() << SignShift) | (uint)value);
                }
            }
        }
    }

    /// <summary>Reads the significance bits of a group of up to four columns of a stripe.</summary>
    /// <param name="reader">The SigProp stream.</param>
    /// <param name="index">The group's first sample.</param>
    /// <param name="columns">The columns in the group.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="found">Receives the samples that became significant, in scan order.</param>
    /// <returns>The number of samples that became significant.</returns>
    private int SigPropGroup(ref JpxHtForwardReader reader, int index, int columns, int rows, scoped Span<int> found)
    {
        var count = 0;
        for (var column = 0; column < columns; column++)
        {
            for (var row = 0; row < rows; row++)
            {
                var i = index + (column * StripeRows) + row;
                if (_flags[i] != 0 || !HasSignificantNeighbour(i, row) || reader.ReadBit() == 0)
                {
                    continue;
                }

                _flags[i] = NewlySignificant;
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
    /// <param name="index">The sample.</param>
    /// <param name="row">The sample's row within its stripe.</param>
    /// <returns><see langword="true"/> when the sample is coded in this pass.</returns>
    private bool HasSignificantNeighbour(int index, int row)
    {
        var up = row == 0 ? index - _stripeStride + LastRow : index - 1;
        var flags = _flags[up - StripeRows] | _flags[up] | _flags[up + StripeRows] | _flags[index - StripeRows] | _flags[index + StripeRows];
        if (row == LastRow && _causal)
        {
            return flags != 0;
        }

        var down = row == LastRow ? index + _stripeStride - LastRow : index + 1;
        return (flags | _flags[down - StripeRows] | _flags[down] | _flags[down + StripeRows]) != 0;
    }
}
