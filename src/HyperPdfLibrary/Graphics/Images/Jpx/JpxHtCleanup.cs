// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Decodes the quads and sample magnitudes of the high-throughput cleanup pass.</summary>
internal static class JpxHtCleanup
{
    /// <summary>The columns of a quad pair.</summary>
    internal const int QuadPairWidth = 4;

    /// <summary>The samples of a quad.</summary>
    internal const int QuadSamples = 4;

    /// <summary>The shift of rho in a VLC table entry.</summary>
    internal const int RhoShift = 4;

    /// <summary>The bits of rho.</summary>
    internal const int RhoMask = 0x0F;

    /// <summary>The shift of the u-offset bit in a VLC table entry.</summary>
    internal const int OffsetShift = 3;

    /// <summary>The shift of the EMB e1 bits in a VLC table entry.</summary>
    internal const int EmbOneShift = 8;

    /// <summary>The shift of the EMB e-k bits in a VLC table entry.</summary>
    internal const int EmbKnownShift = 12;

    /// <summary>The rho bits of a quad's right column, its west neighbours for the next quad.</summary>
    internal const int RightColumn = 0x0C;

    /// <summary>The rho bits of a quad's left column.</summary>
    internal const int LeftColumn = 0x03;

    /// <summary>The context bit of the north-west and north neighbours.</summary>
    internal const int NorthContext = 1;

    /// <summary>The context bit of the west and south-west neighbours.</summary>
    internal const int WestContext = 2;

    /// <summary>The context bit of the north-east and far north-east neighbours.</summary>
    internal const int EastContext = 4;

    /// <summary>The first-row context bits taken from the previous quad's right column.</summary>
    internal const int FirstRowEastBits = 0x06;

    /// <summary>The exponent the context term starts above.</summary>
    internal const int ContextExponentBase = 2;

    /// <summary>The sample positions inside a quad pair: two bits per column, top row in the even bits.</summary>
    internal const int AllPositions = 0xFF;

    /// <summary>The positions of the top row in a quad pair.</summary>
    internal const int TopRowPositions = 0x55;

    /// <summary>One magnitude step at twice scale.</summary>
    internal const uint MagnitudeStep = 2;

    /// <summary>The bits of a sample.</summary>
    internal const int SampleBits = 32;

    /// <summary>Gets the rho of a VLC table entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The significance pattern.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Rho(int entry) => (entry >> RhoShift) & RhoMask;

    /// <summary>Gets the residual mode of a quad pair from its u-offset bits.</summary>
    /// <param name="first">The first quad's entry.</param>
    /// <param name="second">The second quad's entry.</param>
    /// <returns>The mode, 0 to 3.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Offsets(int first, int second) => ((first >> OffsetShift) & 1) | (((second >> OffsetShift) & 1) << 1);

    /// <summary>Gets a quad's context (equations 1 and 2 of T.814).</summary>
    /// <param name="firstRow">Whether the quad is in the first row.</param>
    /// <param name="previous">The rho of the quad to the left, or zero.</param>
    /// <param name="above">The exponents of the row above, column c at c + 1.</param>
    /// <param name="x">The quad's first column.</param>
    /// <returns>The context, 0 to 7.</returns>
    internal static int Context(bool firstRow, int previous, ReadOnlySpan<byte> above, int x)
    {
        if (firstRow)
        {
            return ((previous & LeftColumn) != 0 ? NorthContext : 0) | ((previous >> 1) & FirstRowEastBits);
        }

        var north = (above[x] | above[x + 1]) != 0 ? NorthContext : 0;
        var west = (previous & RightColumn) != 0 ? WestContext : 0;
        var east = (above[x + JpxBlockLayout.QuadWidth] | above[x + JpxBlockLayout.QuadWidth + 1]) != 0 ? EastContext : 0;
        return north | west | east;
    }

    /// <summary>Adds the context term to a quad's bound when it has more than one significant sample (equations 5 and 6).</summary>
    /// <param name="bound">The bound from the residual.</param>
    /// <param name="entry">The quad's entry.</param>
    /// <param name="above">The exponents of the row above.</param>
    /// <param name="x">The quad's first column.</param>
    /// <returns>The bound.</returns>
    internal static int AddContext(int bound, int entry, ReadOnlySpan<byte> above, int x)
    {
        if (BitOperations.PopCount((uint)Rho(entry)) < JpxBlockLayout.QuadWidth)
        {
            return bound;
        }

        var exponent = Math.Max(Math.Max(above[x], above[x + 1]), Math.Max(above[x + JpxBlockLayout.QuadWidth], above[x + JpxBlockLayout.QuadWidth + 1]));
        return bound + Math.Max(exponent - ContextExponentBase, 0);
    }

    /// <summary>Decodes one row of quads.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="streams">The cleanup bit-streams.</param>
    /// <param name="above">The exponents of the row above, column c at c + 1.</param>
    /// <param name="below">Receives the exponents of this row's lower samples.</param>
    /// <param name="y">The row's first sample row.</param>
    /// <param name="depth">The bit depth and exponent limit.</param>
    /// <returns><see langword="false"/> when the data is malformed.</returns>
    internal static bool HtQuadRow(JpxBlockState state, ref JpxHtStreams streams, scoped ReadOnlySpan<byte> above, scoped Span<byte> below, int y, JpxHtDepth depth)
    {
        var firstRow = y == 0;
        var table = firstRow ? JpxHtTables.FirstRow : JpxHtTables.LaterRows;
        var previous = 0;
        for (var x = 0; x < state.Width; x += QuadPairWidth)
        {
            var first = streams.DecodeQuad(table, Context(firstRow, previous, above, x));
            var second = 0;
            if (x + JpxBlockLayout.QuadWidth < state.Width)
            {
                second = streams.DecodeQuad(table, Context(firstRow, Rho(first), above, x + JpxBlockLayout.QuadWidth));
            }

            previous = Rho(second);
            var bounds = streams.DecodeResiduals(Offsets(first, second), firstRow);
            if (!firstRow)
            {
                bounds = new(AddContext(bounds.First, first, above, x), AddContext(bounds.Second, second, above, x + JpxBlockLayout.QuadWidth));
            }

            if (!IsValidPair(state, first, second, bounds, x, y, depth.Limit))
            {
                return false;
            }

            DecodeQuadSamples(state, ref streams, first, bounds.First, new(x, y), below, depth.Planes);
            DecodeQuadSamples(state, ref streams, second, bounds.Second, new(x + JpxBlockLayout.QuadWidth, y), below, depth.Planes);
        }

        return true;
    }

    /// <summary>Checks that a quad pair's significant samples lie inside the code-block and its bounds within the limit.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="first">The first quad's entry.</param>
    /// <param name="second">The second quad's entry.</param>
    /// <param name="bounds">The quads' exponent bounds.</param>
    /// <param name="x">The pair's first column.</param>
    /// <param name="y">The pair's first row.</param>
    /// <param name="limit">The largest bound allowed.</param>
    /// <returns><see langword="true"/> when the pair is well formed.</returns>
    internal static bool IsValidPair(JpxBlockState state, int first, int second, JpxHtBounds bounds, int x, int y, int limit)
    {
        var allowed = AllPositions;
        if (x + QuadPairWidth > state.Width)
        {
            allowed >>= (x + QuadPairWidth - state.Width) * JpxBlockLayout.QuadWidth;
        }

        if (y + JpxBlockLayout.QuadWidth > state.Height)
        {
            allowed &= TopRowPositions;
        }

        var used = Rho(first) | (Rho(second) << RhoShift);
        return (used & ~allowed) == 0 && bounds.First <= limit && bounds.Second <= limit;
    }

    /// <summary>Reads the magnitude and sign of each significant sample of a quad.</summary>
    /// <param name="state">The decoder's mutable state.</param>
    /// <param name="streams">The cleanup bit-streams.</param>
    /// <param name="entry">The quad's entry.</param>
    /// <param name="bound">The quad's exponent bound U.</param>
    /// <param name="corner">The quad's top-left sample.</param>
    /// <param name="below">Receives the exponents of the quad's lower samples.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    internal static void DecodeQuadSamples(JpxBlockState state, ref JpxHtStreams streams, int entry, int bound, JpxHtSample corner, scoped Span<byte> below, int planes)
    {
        var rho = Rho(entry);
        if (rho == 0)
        {
            return;
        }

        var index = JpxBlockLayout.HtIndex(state, corner.X, corner.Y);
        for (var n = 0; n < QuadSamples; n++)
        {
            if (((rho >> n) & 1) == 0)
            {
                continue;
            }

            // A sample whose top magnitude bit the EMB pattern gives reads one bit fewer; the sign is the lowest bit.
            var count = bound - ((entry >> (EmbKnownShift + n)) & 1);
            var bits = streams.ReadMagSgn(count);
            var value = (bits & ((1U << count) - 1)) | ((uint)((entry >> (EmbOneShift + n)) & 1) << count) | 1;
            var i = index + (n & 1) + ((n >> 1) * JpxBlockLayout.StripeRows);

            // The value holds 2(mu - 1) + 1; adding two gives 2 mu + 1, the magnitude with the bin's mid-point.
            state.Values[i] = (int)((bits << JpxBlockLayout.SignShift) | ((value + MagnitudeStep) << (planes - 1)));
            state.Flags[i] = JpxBlockLayout.Significant;
            if ((n & 1) != 0)
            {
                below[corner.X + (n >> 1) + 1] = (byte)(SampleBits - BitOperations.LeadingZeroCount(value));
            }
        }
    }
}
