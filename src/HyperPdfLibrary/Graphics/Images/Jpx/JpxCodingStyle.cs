// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The per-component coding parameters of a COD or COC marker (the SPcod or SPcoc fields).</summary>
/// <param name="Levels">The number of wavelet decomposition levels.</param>
/// <param name="BlockWidthExponent">The code-block width as a power of two.</param>
/// <param name="BlockHeightExponent">The code-block height as a power of two.</param>
/// <param name="BlockStyle">The code-block mode switches.</param>
/// <param name="Reversible">Whether the 5/3 reversible wavelet is used rather than the 9/7 irreversible one.</param>
/// <param name="PrecinctSizes">
/// One byte per resolution: the precinct width exponent in the low nibble and the height exponent in the high nibble.
/// </param>
[DebuggerDisplay("JpxCodingStyle: {Levels} levels, reversible {Reversible}")]
internal sealed record JpxCodingStyle(int Levels, int BlockWidthExponent, int BlockHeightExponent, JpxBlockStyle BlockStyle, bool Reversible, byte[] PrecinctSizes)
{
    /// <summary>The most decomposition levels.</summary>
    internal const int MaxLevels = 32;

    /// <summary>The bit of Scod or Scoc that says precinct sizes follow.</summary>
    internal const int PrecinctsBit = 1;

    /// <summary>The precinct exponent used when none is given: 2^15 covers any tile.</summary>
    private const int DefaultPrecinctExponent = 15;

    /// <summary>The bytes of the fixed SPcod fields.</summary>
    private const int FixedBytes = 5;

    /// <summary>The offset added to the stored code-block exponents.</summary>
    private const int BlockExponentBias = 2;

    /// <summary>The largest code-block side exponent.</summary>
    private const int MaxBlockExponent = 10;

    /// <summary>The largest sum of code-block exponents: blocks hold at most 4096 samples.</summary>
    private const int MaxBlockArea = 12;

    /// <summary>The offset of the code-block height in SPcod.</summary>
    private const int HeightOffset = 2;

    /// <summary>The offset of the code-block style in SPcod.</summary>
    private const int StyleOffset = 3;

    /// <summary>The offset of the transform in SPcod.</summary>
    private const int TransformOffset = 4;

    /// <summary>The bits of a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The bits of the low nibble.</summary>
    private const int NibbleMask = 0x0F;

    /// <summary>The transform value of the reversible 5/3 wavelet.</summary>
    private const int ReversibleTransform = 1;

    /// <summary>Reads the SPcod or SPcoc fields.</summary>
    /// <param name="fields">The fields.</param>
    /// <param name="hasPrecincts">Whether precinct sizes follow, from bit 0 of Scod or Scoc.</param>
    /// <param name="consumed">Receives the bytes read.</param>
    /// <returns>The coding style, or <see langword="null"/> when invalid.</returns>
    internal static JpxCodingStyle? Read(ReadOnlySpan<byte> fields, bool hasPrecincts, out int consumed)
    {
        consumed = 0;
        if (fields.Length < FixedBytes)
        {
            return null;
        }

        var levels = (int)fields[0];
        var width = fields[1] + BlockExponentBias;
        var height = fields[HeightOffset] + BlockExponentBias;
        var count = levels + 1;
        if (!IsValidShape(levels, width, height) || (hasPrecincts && fields.Length < FixedBytes + count))
        {
            return null;
        }

        var sizes = new byte[count];
        if (hasPrecincts)
        {
            fields.Slice(FixedBytes, count).CopyTo(sizes);
            if (!PrecinctsValid(sizes))
            {
                return null;
            }
        }
        else
        {
            sizes.AsSpan().Fill(DefaultPrecinctExponent | (DefaultPrecinctExponent << NibbleBits));
        }

        consumed = FixedBytes + (hasPrecincts ? count : 0);
        return new(levels, width, height, (JpxBlockStyle)fields[StyleOffset], fields[TransformOffset] == ReversibleTransform, sizes);
    }

    /// <summary>Gets the precinct width exponent of a resolution.</summary>
    /// <param name="resolution">The resolution index.</param>
    /// <returns>The exponent.</returns>
    internal int PrecinctWidthExponent(int resolution) => PrecinctSizes[resolution] & NibbleMask;

    /// <summary>Gets the precinct height exponent of a resolution.</summary>
    /// <param name="resolution">The resolution index.</param>
    /// <returns>The exponent.</returns>
    internal int PrecinctHeightExponent(int resolution) => PrecinctSizes[resolution] >> NibbleBits;

    /// <summary>Checks the decomposition levels and code-block size against the limits of ISO 15444-1 A.6.1.</summary>
    /// <param name="levels">The decomposition levels.</param>
    /// <param name="width">The code-block width exponent.</param>
    /// <param name="height">The code-block height exponent.</param>
    /// <returns><see langword="true"/> when within the limits.</returns>
    private static bool IsValidShape(int levels, int width, int height) =>
        levels <= MaxLevels && width <= MaxBlockExponent && height <= MaxBlockExponent && width + height <= MaxBlockArea;

    /// <summary>Checks that every resolution above the lowest has precincts at least two samples on each side.</summary>
    /// <param name="sizes">The packed precinct exponents.</param>
    /// <returns><see langword="true"/> when valid.</returns>
    private static bool PrecinctsValid(ReadOnlySpan<byte> sizes)
    {
        for (var i = 1; i < sizes.Length; i++)
        {
            if ((sizes[i] & NibbleMask) == 0 || (sizes[i] >> NibbleBits) == 0)
            {
                return false;
            }
        }

        return true;
    }
}
