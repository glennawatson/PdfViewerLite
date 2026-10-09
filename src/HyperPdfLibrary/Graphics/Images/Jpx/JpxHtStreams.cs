// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// The three bit-streams of an HT cleanup segment (T.814): MEL events, the VLC codes of quads and their
/// unsigned residuals, and the MagSgn bits of significant samples.
/// </summary>
internal ref struct JpxHtStreams
{
    /// <summary>The VLC bits a table lookup reads.</summary>
    private const int LookupBits = 7;

    /// <summary>The mask of the bits a table lookup reads.</summary>
    private const uint LookupMask = (1U << LookupBits) - 1;

    /// <summary>The codeword length bits of a table entry.</summary>
    private const int LengthMask = 0x07;

    /// <summary>The bits that select a residual prefix.</summary>
    private const uint PrefixMask = 0x07;

    /// <summary>The prefix length bits of a residual prefix entry.</summary>
    private const int PrefixLengthMask = 0x03;

    /// <summary>The shift of the suffix length in a residual prefix entry.</summary>
    private const int SuffixShift = 2;

    /// <summary>The suffix length bits of a residual prefix entry, after the shift.</summary>
    private const int SuffixMask = 0x07;

    /// <summary>The shift of the prefix value in a residual prefix entry.</summary>
    private const int ValueShift = 5;

    /// <summary>The longest prefix, which takes a suffix.</summary>
    private const int LongPrefix = 3;

    /// <summary>The residual offset added when the MEL event says both residuals are above two.</summary>
    private const int BothLarge = 2;

    /// <summary>The MEL decoder.</summary>
    private JpxHtMelDecoder _mel;

    /// <summary>The VLC reader.</summary>
    private JpxHtReverseReader _vlc;

    /// <summary>The MagSgn reader.</summary>
    private JpxHtForwardReader _magSgn;

    /// <summary>Initializes a new instance of the <see cref="JpxHtStreams"/> struct.</summary>
    /// <param name="mel">The MEL decoder.</param>
    /// <param name="vlc">The VLC reader.</param>
    /// <param name="magSgn">The MagSgn reader.</param>
    internal JpxHtStreams(JpxHtMelDecoder mel, JpxHtReverseReader vlc, JpxHtForwardReader magSgn)
    {
        _mel = mel;
        _vlc = vlc;
        _magSgn = magSgn;
    }

    /// <summary>
    /// Gets the unsigned residual prefixes (table 3), indexed by the next three VLC bits: the prefix length (bits 0 and 1),
    /// the suffix length (bits 2 to 4) and the prefix value (bits 5 to 7).
    /// </summary>
    private static ReadOnlySpan<byte> Prefixes => [0xB7, 0x21, 0x42, 0x21, 0x67, 0x21, 0x42, 0x21];

    /// <summary>Decodes one quad's VLC code; a zero-context quad first takes a MEL event and is insignificant when it is zero.</summary>
    /// <param name="table">The VLC table of the quad's row.</param>
    /// <param name="context">The quad's context, 0 to 7.</param>
    /// <returns>The table entry, or zero for an insignificant zero-context quad.</returns>
    internal int DecodeQuad(ReadOnlySpan<ushort> table, int context)
    {
        if (context == 0 && _mel.Next() == 0)
        {
            return 0;
        }

        int entry = table[(context << LookupBits) | (int)(_vlc.Peek() & LookupMask)];
        _vlc.Skip(entry & LengthMask);
        return entry;
    }

    /// <summary>Decodes the unsigned residuals of a quad pair, each plus one (the exponent bound before the context term).</summary>
    /// <param name="mode">The u-offset bits: bit 0 for the first quad, bit 1 for the second.</param>
    /// <param name="firstRow">Whether the pair is in the first row of quads.</param>
    /// <returns>The two bounds.</returns>
    internal JpxHtBounds DecodeResiduals(int mode, bool firstRow)
    {
        if (mode == 0)
        {
            return new(1, 1);
        }

        var bits = _vlc.Peek();
        var used = 0;
        var result = mode != JpxHtBounds.Both ? ReadSingle(ref bits, ref used, mode) : ReadBoth(ref bits, ref used, firstRow);
        _vlc.Skip(used);
        return result;
    }

    /// <summary>Reads a sample's magnitude and sign bits.</summary>
    /// <param name="count">The bits, at most 31.</param>
    /// <returns>The bits, the sign lowest; with no bits, the next bit unconsumed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint ReadMagSgn(int count)
    {
        var bits = _magSgn.Peek();
        _magSgn.Skip(count);
        return bits;
    }

    /// <summary>Reads the one residual of a pair where only one quad has a u-offset.</summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <param name="mode">1 when the first quad has the offset, 2 when the second does.</param>
    /// <returns>The two bounds.</returns>
    private static JpxHtBounds ReadSingle(ref uint bits, ref int used, int mode)
    {
        var single = ReadResidual(ref bits, ref used) + 1;
        return mode == 1 ? new(single, 1) : new(1, single);
    }

    /// <summary>Reads one residual: its prefix, then its suffix.</summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <returns>The residual.</returns>
    private static int ReadResidual(ref uint bits, ref int used)
    {
        var entry = ReadPrefix(ref bits, ref used);
        return ReadSuffix(entry, ref bits, ref used);
    }

    /// <summary>Reads a residual prefix.</summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <returns>The prefix entry.</returns>
    private static int ReadPrefix(ref uint bits, ref int used)
    {
        int entry = Prefixes[(int)(bits & PrefixMask)];
        var length = entry & PrefixLengthMask;
        bits >>= length;
        used += length;
        return entry;
    }

    /// <summary>Reads a residual suffix and adds it to the prefix value.</summary>
    /// <param name="entry">The prefix entry.</param>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <returns>The residual.</returns>
    private static int ReadSuffix(int entry, ref uint bits, ref int used)
    {
        var length = (entry >> SuffixShift) & SuffixMask;
        var suffix = (int)(bits & ((1U << length) - 1));
        bits >>= length;
        used += length;
        return (entry >> ValueShift) + suffix;
    }

    /// <summary>Reads two residuals coded as both prefixes, then both suffixes.</summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <param name="offset">What each residual adds to make its bound.</param>
    /// <returns>The two bounds.</returns>
    private static JpxHtBounds ReadPair(ref uint bits, ref int used, int offset)
    {
        var first = ReadPrefix(ref bits, ref used);
        var second = ReadPrefix(ref bits, ref used);
        var a = ReadSuffix(first, ref bits, ref used);
        var b = ReadSuffix(second, ref bits, ref used);
        return new(a + offset, b + offset);
    }

    /// <summary>
    /// Reads the residuals of a first-row pair whose MEL event is zero: when the first prefix is long the second residual
    /// is one bit, one or two; otherwise both are coded as usual.
    /// </summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <returns>The two bounds.</returns>
    private static JpxHtBounds ReadFirstRowPair(ref uint bits, ref int used)
    {
        var first = ReadPrefix(ref bits, ref used);
        if ((first & PrefixLengthMask) < LongPrefix)
        {
            var second = ReadPrefix(ref bits, ref used);
            var a = ReadSuffix(first, ref bits, ref used);
            var b = ReadSuffix(second, ref bits, ref used);
            return new(a + 1, b + 1);
        }

        var small = (int)(bits & 1) + 1;
        bits >>= 1;
        used++;
        return new(ReadSuffix(first, ref bits, ref used) + 1, small + 1);
    }

    /// <summary>Reads the residuals of a pair where both quads have a u-offset; in the first row a MEL event says whether both are above two.</summary>
    /// <param name="bits">The VLC bits, consumed from the bottom.</param>
    /// <param name="used">The bits consumed so far.</param>
    /// <param name="firstRow">Whether the pair is in the first row of quads.</param>
    /// <returns>The two bounds.</returns>
    private JpxHtBounds ReadBoth(ref uint bits, ref int used, bool firstRow)
    {
        if (!firstRow)
        {
            return ReadPair(ref bits, ref used, 1);
        }

        return _mel.Next() == 1 ? ReadPair(ref bits, ref used, BothLarge + 1) : ReadFirstRowPair(ref bits, ref used);
    }
}
