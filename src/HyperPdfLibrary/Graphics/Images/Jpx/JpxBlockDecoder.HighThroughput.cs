// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <content>
/// The high-throughput block decoder (T.814 | ISO/IEC 15444-15): one HT set made of a cleanup pass and, when sent, the
/// SigProp and MagRef refinement passes. Samples are rebuilt sign-magnitude at the same twice scale as the regular
/// decoder, with the mid-point of the remaining interval, then turned into signed values for <see cref="Store"/>.
/// Like PDFium, a code-block with a region-of-interest shift or more than 30 magnitude bit-planes is left at zero, and
/// a malformed code-block keeps what decoded before the damage.
/// </content>
internal sealed partial class JpxBlockDecoder
{
    /// <summary>The most magnitude bit-planes the 32-bit sign-magnitude samples hold.</summary>
    private const int MaxHtMagnitude = 30;

    /// <summary>The widest code-block (ISO 15444-1 A.6.1).</summary>
    private const int MaxBlockWidth = 1024;

    /// <summary>The bytes of a line of exponents: one per column, a zero column each side, and room for the far neighbour.</summary>
    private const int LineBytes = MaxBlockWidth + StripeRows;

    /// <summary>The bytes at the end of a cleanup segment that hold Scup, and the smallest Scup.</summary>
    private const int SuffixLengthBytes = 2;

    /// <summary>The largest MEL and VLC length, Scup.</summary>
    private const int MaxSuffixLength = 4079;

    /// <summary>The shift of the high bits of Scup, held in the segment's last byte.</summary>
    private const int SuffixHighShift = 4;

    /// <summary>The low nibble of the segment's second-last byte, which holds the low bits of Scup.</summary>
    private const int SuffixLowMask = 0x0F;

    /// <summary>The passes of an HT set with both refinement passes.</summary>
    private const int FullHtSet = 3;

    /// <summary>The fewest bit-planes that leave room for the refinement plane.</summary>
    private const int RefinablePlanes = 2;

    /// <summary>The fill byte of the MagSgn stream.</summary>
    private const byte MagSgnFill = 0xFF;

    /// <summary>The flag of a sample that became significant in the SigProp pass.</summary>
    private const byte NewlySignificant = 2;

    /// <summary>The magnitude bits of a sign-magnitude sample.</summary>
    private const int MagnitudeMask = int.MaxValue;

    /// <summary>Counts the passes of the first HT set that can be decoded.</summary>
    /// <param name="cleanup">The cleanup segment.</param>
    /// <param name="refinement">The refinement segment, or an empty one.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    /// <returns>1 for the cleanup pass alone, 2 with SigProp, 3 with SigProp and MagRef.</returns>
    private static int HtPasses(JpxSegment cleanup, JpxSegment refinement, int planes)
    {
        var passes = Math.Min(cleanup.Passes + refinement.Passes, FullHtSet);

        // Refinement passes with no bytes, or with no bit-plane left below the cleanup pass, are dropped, as PDFium does.
        return refinement.Length == 0 || planes < RefinablePlanes ? Math.Min(passes, 1) : passes;
    }

    /// <summary>Decodes one high-throughput code-block into its tile-component buffer.</summary>
    /// <param name="tile">The tile, which holds the code-block's segments and chunks.</param>
    /// <param name="block">The code-block.</param>
    /// <param name="band">The code-block's sub-band.</param>
    /// <param name="data">The tile's packet data.</param>
    /// <param name="target">Where the coefficients go.</param>
    private void DecodeHighThroughput(JpxTile tile, JpxCodeBlock block, in JpxBandLayout band, byte[] data, in JpxBlockTarget target)
    {
        if (target.RoiShift != 0 || band.Magnitude > MaxHtMagnitude || block.Area.Width > MaxBlockWidth)
        {
            return;
        }

        var cleanup = tile.Segments[block.FirstSegment];
        var refinement = cleanup.Next >= 0 ? tile.Segments[cleanup.Next] : default;
        var passes = HtPasses(cleanup, refinement, block.BitPlanes);
        var refinementLength = passes > 1 ? refinement.Length : 0;
        if (cleanup.Length < SuffixLengthBytes || cleanup.Length + refinementLength > block.DataLength)
        {
            return;
        }

        Prepare(block.Area.Width, block.Area.Height, band.Orientation, target.Style);
        var source = Gather(tile, block, data, out var offset);
        var bytes = source.AsSpan(offset, cleanup.Length + refinementLength);
        if (HtCleanup(bytes[..cleanup.Length], block.BitPlanes, band.Magnitude) && passes > 1)
        {
            HtRefine(bytes[cleanup.Length..], passes, block.BitPlanes);
        }

        ToSigned();
        Store(block, band, target);
    }

    /// <summary>Runs the refinement passes.</summary>
    /// <param name="segment">The refinement segment.</param>
    /// <param name="passes">The passes of the HT set, 2 or 3.</param>
    /// <param name="planes">The code-block's bit-planes.</param>
    private void HtRefine(ReadOnlySpan<byte> segment, int passes, int planes)
    {
        if (passes == FullHtSet)
        {
            HtMagRef(segment, planes);
        }

        HtSigProp(segment, planes);
    }

    /// <summary>Gets a sample's index in the stripe-ordered state buffers.</summary>
    /// <param name="x">The column.</param>
    /// <param name="y">The row.</param>
    /// <returns>The index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int HtIndex(int x, int y) => (((y / StripeRows) + 1) * _stripeStride) + ((x + 1) * StripeRows) + (y % StripeRows);

    /// <summary>Turns the sign-magnitude samples into signed values.</summary>
    private void ToSigned()
    {
        foreach (ref var value in _values.AsSpan(0, _length))
        {
            if (value < 0)
            {
                value = -(value & MagnitudeMask);
            }
        }
    }

    /// <summary>Reads Scup and runs the cleanup pass.</summary>
    /// <param name="segment">The cleanup segment.</param>
    /// <param name="planes">The code-block's bit-planes, p.</param>
    /// <param name="magnitude">The sub-band's magnitude bit-planes, Mb.</param>
    /// <returns><see langword="false"/> when the segment is malformed.</returns>
    private bool HtCleanup(ReadOnlySpan<byte> segment, int planes, int magnitude)
    {
        var suffixLength = (segment[^1] << SuffixHighShift) | (segment[^SuffixLengthBytes] & SuffixLowMask);
        if (suffixLength < SuffixLengthBytes || suffixLength > segment.Length || suffixLength > MaxSuffixLength)
        {
            return false;
        }

        var streams = new JpxHtStreams(
            new(segment, suffixLength),
            JpxHtReverseReader.ForVlc(segment, suffixLength),
            new(segment[..^suffixLength], MagSgnFill));

        // Two lines of exponents, used in turn: the row above each row of quads, and that row's own lower samples.
        Span<byte> lines = stackalloc byte[LineBytes * SuffixLengthBytes];

        // A quad's exponent bound may not pass the missing bit-planes plus two, as PDFium checks.
        var depth = new JpxHtDepth(planes, magnitude + SuffixLengthBytes - planes);
        var current = 0;
        for (var y = 0; y < _height; y += QuadWidth)
        {
            var above = lines.Slice((1 - current) * LineBytes, LineBytes);
            var below = lines.Slice(current * LineBytes, LineBytes);
            below.Clear();
            if (!HtQuadRow(ref streams, above, below, y, depth))
            {
                return false;
            }

            current = 1 - current;
        }

        return true;
    }
}
