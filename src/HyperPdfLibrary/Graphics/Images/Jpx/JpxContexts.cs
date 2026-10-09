// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The coding context tables of the block coder (ISO 15444-1 annex D). A coefficient's neighbourhood is one byte: the
/// significant horizontal neighbours in bits 0-1, the vertical ones in bits 2-3 and the diagonal ones in bits 4-6.
/// </summary>
internal static class JpxContexts
{
    /// <summary>The contexts used by the block coder.</summary>
    internal const int Count = 19;

    /// <summary>The first sign-coding context.</summary>
    internal const int FirstSign = 9;

    /// <summary>The magnitude refinement context of a first refinement with no significant neighbours.</summary>
    internal const int FirstRefinement = 14;

    /// <summary>The magnitude refinement context of a first refinement with significant neighbours.</summary>
    internal const int FirstRefinementNear = 15;

    /// <summary>The magnitude refinement context of later refinements.</summary>
    internal const int LaterRefinement = 16;

    /// <summary>The run-length context.</summary>
    internal const int RunLength = 17;

    /// <summary>The uniform context.</summary>
    internal const int Uniform = 18;

    /// <summary>The neighbourhood codes per orientation.</summary>
    internal const int Neighbourhoods = 128;

    /// <summary>The neighbourhood increment of a horizontal neighbour.</summary>
    internal const int Horizontal = 1;

    /// <summary>The neighbourhood increment of a vertical neighbour.</summary>
    internal const int Vertical = 4;

    /// <summary>The neighbourhood increment of a diagonal neighbour.</summary>
    internal const int Diagonal = 16;

    /// <summary>The bit of a sign entry that inverts the decoded sign.</summary>
    internal const int SignFlip = 0x80;

    /// <summary>The mask of the context in a sign entry.</summary>
    internal const int SignContextMask = 0x7F;

    /// <summary>The initial state of the uniform context.</summary>
    internal const int UniformState = 46 << 1;

    /// <summary>The initial state of the run-length context.</summary>
    internal const int RunLengthState = 3 << 1;

    /// <summary>The initial state of the first zero-coding context.</summary>
    internal const int ZeroState = 4 << 1;

    /// <summary>
    /// The orientation of the HL sub-band (horizontally high-pass), whose zero-coding table swaps the horizontal and
    /// vertical counts; the LL and LH sub-bands share the other table (table D.1).
    /// </summary>
    private const int SwappedOrientation = 1;

    /// <summary>The orientation of the HH sub-band, which has its own zero-coding table.</summary>
    private const int DiagonalOrientation = 3;

    /// <summary>The orientations.</summary>
    private const int Orientations = 4;

    /// <summary>The mask of a two-bit neighbour count.</summary>
    private const int CountMask = 3;

    /// <summary>The shift of the vertical count.</summary>
    private const int VerticalShift = 2;

    /// <summary>The shift of the diagonal count.</summary>
    private const int DiagonalShift = 4;

    /// <summary>The most horizontal or vertical neighbours.</summary>
    private const int Two = 2;

    /// <summary>The most diagonal neighbours.</summary>
    private const int DiagonalLimit = 4;

    /// <summary>The diagonal counts, zero to four.</summary>
    private const int DiagonalCounts = 5;

    /// <summary>The entries of the axis table per primary count.</summary>
    private const int AxisStride = 15;

    /// <summary>Gets the zero-coding context of every neighbourhood, 128 per orientation.</summary>
    internal static byte[] ZeroCoding { get; } = BuildZeroCoding();

    /// <summary>
    /// Gets the sign-coding entries indexed by <c>(h + 1) * 3 + (v + 1)</c> for the clamped horizontal and vertical sign
    /// contributions: the context, with <see cref="SignFlip"/> set when the decoded bit is inverted (table D.3).
    /// </summary>
    internal static ReadOnlySpan<byte> Sign => [0x8D, 0x8C, 0x8B, 0x8A, 0x09, 0x0A, 0x0B, 0x0C, 0x0D];

    /// <summary>
    /// Gets the zero-coding contexts of the LL, HL and LH sub-bands (table D.1), indexed by
    /// <c>primary * 15 + secondary * 5 + diagonal</c>.
    /// </summary>
    private static ReadOnlySpan<byte> AxisTable =>
    [
        0x00, 0x01, 0x02, 0x02, 0x02, 0x03, 0x03, 0x03, 0x03, 0x03, 0x04, 0x04, 0x04, 0x04, 0x04,
        0x05, 0x06, 0x06, 0x06, 0x06, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07, 0x07,
        0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08, 0x08,
    ];

    /// <summary>Gets the zero-coding contexts of the HH sub-band (table D.1), indexed by <c>(h + v) * 5 + diagonal</c>.</summary>
    private static ReadOnlySpan<byte> DiagonalTable =>
    [
        0x00, 0x03, 0x06, 0x08, 0x08, 0x01, 0x04, 0x07, 0x08, 0x08, 0x02, 0x05, 0x07, 0x08, 0x08,
        0x02, 0x05, 0x07, 0x08, 0x08, 0x02, 0x05, 0x07, 0x08, 0x08,
    ];

    /// <summary>Builds the zero-coding contexts of every orientation (table D.1).</summary>
    /// <returns>The table.</returns>
    private static byte[] BuildZeroCoding()
    {
        var table = new byte[Orientations * Neighbourhoods];
        for (var orientation = 0; orientation < Orientations; orientation++)
        {
            for (var code = 0; code < Neighbourhoods; code++)
            {
                var h = code & CountMask;
                var v = (code >> VerticalShift) & CountMask;
                var d = code >> DiagonalShift;
                if (h > Two || v > Two || d > DiagonalLimit)
                {
                    continue;
                }

                table[(orientation * Neighbourhoods) + code] = orientation switch
                {
                    DiagonalOrientation => DiagonalTable[((h + v) * DiagonalCounts) + d],
                    SwappedOrientation => AxisTable[(v * AxisStride) + (h * DiagonalCounts) + d],
                    _ => AxisTable[(h * AxisStride) + (v * DiagonalCounts) + d],
                };
            }
        }

        return table;
    }
}
