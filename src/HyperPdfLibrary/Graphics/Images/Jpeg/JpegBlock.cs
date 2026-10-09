// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>
/// The layout of an 8x8 coefficient block. Coefficients are stored transposed: the horizontal frequency selects the row
/// and the vertical frequency the column, so the first inverse-DCT pass runs along rows of vectors and one transpose joins the passes.
/// </summary>
internal static class JpegBlock
{
    /// <summary>The side of a block.</summary>
    internal const int Side = 8;

    /// <summary>The coefficients in a block.</summary>
    internal const int Length = 64;

    /// <summary>The offset of row 1 of a block.</summary>
    internal const int Row1 = Side;

    /// <summary>The offset of row 2 of a block.</summary>
    internal const int Row2 = 2 * Side;

    /// <summary>The offset of row 3 of a block.</summary>
    internal const int Row3 = 3 * Side;

    /// <summary>The offset of row 4 of a block.</summary>
    internal const int Row4 = 4 * Side;

    /// <summary>The offset of row 5 of a block.</summary>
    internal const int Row5 = 5 * Side;

    /// <summary>The offset of row 6 of a block.</summary>
    internal const int Row6 = 6 * Side;

    /// <summary>The offset of row 7 of a block.</summary>
    internal const int Row7 = 7 * Side;

    /// <summary>Gets the storage position of each zigzag index from 0 to 63, followed by 16 entries that point at the last coefficient.</summary>
    internal static byte[] Zigzag { get; } = BuildZigzagStorage();

    /// <summary>Gets the row-major position of each zigzag index, as in the JPEG standard.</summary>
    private static ReadOnlySpan<byte> NaturalOrder =>
    [
        0x00, 0x01, 0x08, 0x10, 0x09, 0x02, 0x03, 0x0A,
        0x11, 0x18, 0x20, 0x19, 0x12, 0x0B, 0x04, 0x05,
        0x0C, 0x13, 0x1A, 0x21, 0x28, 0x30, 0x29, 0x22,
        0x1B, 0x14, 0x0D, 0x06, 0x07, 0x0E, 0x15, 0x1C,
        0x23, 0x2A, 0x31, 0x38, 0x39, 0x32, 0x2B, 0x24,
        0x1D, 0x16, 0x0F, 0x17, 0x1E, 0x25, 0x2C, 0x33,
        0x3A, 0x3B, 0x34, 0x2D, 0x26, 0x1F, 0x27, 0x2E,
        0x35, 0x3C, 0x3D, 0x36, 0x2F, 0x37, 0x3E, 0x3F,
    ];

    /// <summary>Builds the transposed storage positions.</summary>
    /// <returns>The positions, with padding entries.</returns>
    private static byte[] BuildZigzagStorage()
    {
        const int Padding = 16;
        var table = new byte[Length + Padding];
        for (var i = 0; i < Length; i++)
        {
            var natural = NaturalOrder[i];
            table[i] = (byte)(((natural % Side) * Side) + (natural / Side));
        }

        table.AsSpan(Length).Fill(table[Length - 1]);
        return table;
    }
}
