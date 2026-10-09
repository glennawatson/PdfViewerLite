// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The three coding passes of the block decoder.</summary>
internal sealed partial class JpxBlockDecoder
{
    /// <summary>The flag of a significant coefficient.</summary>
    private const byte Significant = 1;

    /// <summary>The flag of a coefficient coded in the current bit-plane's significance pass.</summary>
    private const byte Visited = 2;

    /// <summary>The flag of a coefficient refined at least once.</summary>
    private const byte Refined = 4;

    /// <summary>The flag of a negative coefficient.</summary>
    private const byte Negative = 8;

    /// <summary>The shift that turns <see cref="Negative"/> into two.</summary>
    private const int NegativeShift = 2;

    /// <summary>The significant flags of a whole stripe column.</summary>
    private const uint ColumnSignificant = 0x01010101;

    /// <summary>The visited flags of a whole stripe column, inverted to clear them.</summary>
    private const uint ColumnNotVisited = ~0x02020202U;

    /// <summary>The symbols of a segmentation marker.</summary>
    private const int SegmentationSymbols = 4;

    /// <summary>The sign-table entries per horizontal contribution.</summary>
    private const int SignRow = 3;

    /// <summary>The shift of the sign-flip bit in a sign-table entry.</summary>
    private const int SignFlipShift = 7;

    /// <summary>Gets a coefficient's sign contribution: 1 when positive and significant, -1 when negative, else 0.</summary>
    /// <param name="flags">The neighbour's flags.</param>
    /// <returns>The contribution.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Contribution(byte flags) => (flags & Significant) == 0 ? 0 : 1 - ((flags & Negative) >> NegativeShift);

    /// <summary>Gets the magnitude refinement context (table D.4).</summary>
    /// <param name="flags">The coefficient's flags.</param>
    /// <param name="neighbours">The coefficient's neighbourhood code.</param>
    /// <returns>The context.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int RefinementContext(byte flags, byte neighbours)
    {
        if ((flags & Refined) != 0)
        {
            return JpxContexts.LaterRefinement;
        }

        return neighbours != 0 ? JpxContexts.FirstRefinementNear : JpxContexts.FirstRefinement;
    }

    /// <summary>Runs a significance propagation pass.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    private void Significance(ref JpxMqDecoder coder, int plane, bool raw)
    {
        var value = (1 << plane) | (1 << (plane - 1));
        for (var stripe = 0; stripe < _stripes; stripe++)
        {
            var rows = Math.Min(StripeRows, _height - (stripe * StripeRows));
            var index = ((stripe + 1) * _stripeStride) + StripeRows;
            for (var x = 0; x < _width; x++)
            {
                // A column with no significant neighbours has nothing to code in this pass.
                if (ReadColumn(_neighbours, index) != 0)
                {
                    SignificanceColumn(ref coder, index, rows, value, raw);
                }

                index += StripeRows;
            }
        }
    }

    /// <summary>Runs the significance pass over one stripe column.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="value">The value a newly significant coefficient takes.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    private void SignificanceColumn(ref JpxMqDecoder coder, int index, int rows, int value, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = _flags[i];
            var neighbours = _neighbours[i];
            if ((flags & Significant) != 0 || neighbours == 0)
            {
                continue;
            }

            var bit = raw ? coder.DecodeRaw() : coder.Decode(ref _contexts[JpxContexts.ZeroCoding[_zeroTable + neighbours]]);
            if (bit == 0)
            {
                _flags[i] = (byte)(flags | Visited);
            }
            else
            {
                var negative = raw ? coder.DecodeRaw() != 0 : DecodeSign(ref coder, i, row);
                MakeSignificant(i, row, value, negative);
            }
        }
    }

    /// <summary>Runs a magnitude refinement pass.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    private void Refinement(ref JpxMqDecoder coder, int plane, bool raw)
    {
        var half = 1 << (plane - 1);
        for (var stripe = 0; stripe < _stripes; stripe++)
        {
            var rows = Math.Min(StripeRows, _height - (stripe * StripeRows));
            var index = ((stripe + 1) * _stripeStride) + StripeRows;
            for (var x = 0; x < _width; x++)
            {
                if ((ReadColumn(_flags, index) & ColumnSignificant) != 0)
                {
                    RefinementColumn(ref coder, index, rows, half, raw);
                }

                index += StripeRows;
            }
        }
    }

    /// <summary>Runs the refinement pass over one stripe column.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="half">Half the bit-plane's weight, added or taken away.</param>
    /// <param name="raw">Whether the pass is raw bypass data.</param>
    private void RefinementColumn(ref JpxMqDecoder coder, int index, int rows, int half, bool raw)
    {
        for (var row = 0; row < rows; row++)
        {
            var i = index + row;
            var flags = _flags[i];
            if ((flags & (Significant | Visited)) != Significant)
            {
                continue;
            }

            var bit = raw ? coder.DecodeRaw() : coder.Decode(ref _contexts[RefinementContext(flags, _neighbours[i])]);
            var current = _values[i];
            _values[i] = current + ((bit ^ (current < 0 ? 1 : 0)) != 0 ? half : -half);
            _flags[i] = (byte)(flags | Refined);
        }
    }

    /// <summary>Runs a cleanup pass, then reads the segmentation symbol when the mode asks for one.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="plane">The bit-plane plus one.</param>
    /// <param name="segmentation">Whether a segmentation symbol follows.</param>
    private void Cleanup(ref JpxMqDecoder coder, int plane, bool segmentation)
    {
        var value = (1 << plane) | (1 << (plane - 1));
        for (var stripe = 0; stripe < _stripes; stripe++)
        {
            var rows = Math.Min(StripeRows, _height - (stripe * StripeRows));
            var index = ((stripe + 1) * _stripeStride) + StripeRows;
            for (var x = 0; x < _width; x++)
            {
                CleanupColumn(ref coder, index, rows, value);
                index += StripeRows;
            }
        }

        if (!segmentation)
        {
            return;
        }

        // The symbol should be 1010; as PDFium does, a wrong symbol is not treated as an error.
        for (var i = 0; i < SegmentationSymbols; i++)
        {
            _ = coder.Decode(ref _contexts[JpxContexts.Uniform]);
        }
    }

    /// <summary>Runs the cleanup pass over one stripe column, using run-length coding when the whole column is quiet.</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="index">The column's first coefficient.</param>
    /// <param name="rows">The rows of the stripe.</param>
    /// <param name="value">The value a newly significant coefficient takes.</param>
    private void CleanupColumn(ref JpxMqDecoder coder, int index, int rows, int value)
    {
        var start = 0;
        if (rows == StripeRows && ReadColumn(_flags, index) == 0 && ReadColumn(_neighbours, index) == 0)
        {
            if (coder.Decode(ref _contexts[JpxContexts.RunLength]) == 0)
            {
                return;
            }

            start = coder.Decode(ref _contexts[JpxContexts.Uniform]) << 1;
            start |= coder.Decode(ref _contexts[JpxContexts.Uniform]);
            MakeSignificant(index + start, start, value, DecodeSign(ref coder, index + start, start));
            start++;
        }

        for (var row = start; row < rows; row++)
        {
            var i = index + row;
            if ((_flags[i] & (Significant | Visited)) == 0
                && coder.Decode(ref _contexts[JpxContexts.ZeroCoding[_zeroTable + _neighbours[i]]]) != 0)
            {
                MakeSignificant(i, row, value, DecodeSign(ref coder, i, row));
            }
        }

        WriteColumn(_flags, index, ReadColumn(_flags, index) & ColumnNotVisited);
    }

    /// <summary>Decodes the sign of a coefficient that has just become significant (D.3.2).</summary>
    /// <param name="coder">The decoder.</param>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns><see langword="true"/> when negative.</returns>
    private bool DecodeSign(ref JpxMqDecoder coder, int i, int row)
    {
        var horizontal = Math.Clamp(Contribution(_flags[i - StripeRows]) + Contribution(_flags[i + StripeRows]), -1, 1);
        var below = _causal && row == LastRow ? 0 : Contribution(_flags[Below(i, row)]);
        var vertical = Math.Clamp(Contribution(_flags[Above(i, row)]) + below, -1, 1);
        var entry = JpxContexts.Sign[((horizontal + 1) * SignRow) + vertical + 1];
        var bit = coder.Decode(ref _contexts[entry & JpxContexts.SignContextMask]);
        return (bit ^ (entry >> SignFlipShift)) != 0;
    }

    /// <summary>Makes a coefficient significant and tells its neighbours.</summary>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <param name="value">The magnitude it takes, at twice scale.</param>
    /// <param name="negative">Whether it is negative.</param>
    private void MakeSignificant(int i, int row, int value, bool negative)
    {
        _values[i] = negative ? -value : value;
        _flags[i] = (byte)(_flags[i] | Significant | Visited | (negative ? Negative : 0));
        _neighbours[i - StripeRows] += JpxContexts.Horizontal;
        _neighbours[i + StripeRows] += JpxContexts.Horizontal;

        // In the vertically causal mode the stripe above never sees this stripe.
        if (!_causal || row != 0)
        {
            var above = Above(i, row);
            _neighbours[above] += JpxContexts.Vertical;
            _neighbours[above - StripeRows] += JpxContexts.Diagonal;
            _neighbours[above + StripeRows] += JpxContexts.Diagonal;
        }

        var below = Below(i, row);
        _neighbours[below] += JpxContexts.Vertical;
        _neighbours[below - StripeRows] += JpxContexts.Diagonal;
        _neighbours[below + StripeRows] += JpxContexts.Diagonal;
    }

    /// <summary>Gets the coefficient above another in the stripe layout.</summary>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns>The index of the coefficient above.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Above(int i, int row) => row > 0 ? i - 1 : i - _stripeStride + LastRow;

    /// <summary>Gets the coefficient below another in the stripe layout.</summary>
    /// <param name="i">The coefficient.</param>
    /// <param name="row">The row within the stripe.</param>
    /// <returns>The index of the coefficient below.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Below(int i, int row) => row < LastRow ? i + 1 : i + _stripeStride - LastRow;
}
