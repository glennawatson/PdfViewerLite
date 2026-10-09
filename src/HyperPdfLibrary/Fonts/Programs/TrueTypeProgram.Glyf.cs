// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Programs;

/// <content>Glyph record lookup and composite glyphs.</content>
public sealed partial class TrueTypeProgram
{
    /// <summary>The composite flag for 16-bit arguments.</summary>
    private const int ArgsAreWords = 0x0001;

    /// <summary>The composite flag for offsets rather than point numbers.</summary>
    private const int ArgsAreXYValues = 0x0002;

    /// <summary>The composite flag for one uniform scale.</summary>
    private const int HasScale = 0x0008;

    /// <summary>The composite flag that another component follows.</summary>
    private const int MoreComponents = 0x0020;

    /// <summary>The composite flag for separate x and y scales.</summary>
    private const int HasXYScale = 0x0040;

    /// <summary>The composite flag for a 2x2 matrix.</summary>
    private const int HasTwoByTwo = 0x0080;

    /// <summary>The composite flag that scales the offset by the component's matrix.</summary>
    private const int ScaledOffset = 0x0800;

    /// <summary>The composite flag that leaves the offset unscaled.</summary>
    private const int UnscaledOffset = 0x1000;

    /// <summary>The size of a component's flags and glyph index.</summary>
    private const int ComponentHeader = 4;

    /// <summary>The size of one 2.14 value.</summary>
    private const int F2Dot14Size = 2;

    /// <summary>The size of the two-value scale.</summary>
    private const int XYScaleSize = 4;

    /// <summary>The size of the 2x2 matrix.</summary>
    private const int TwoByTwoSize = 8;

    /// <summary>The index of the third 2x2 value.</summary>
    private const int ThirdValue = 2;

    /// <summary>The index of the fourth 2x2 value.</summary>
    private const int FourthValue = 3;

    /// <summary>Reads a component's offset and matrix. Point-matched components are placed without an offset.</summary>
    /// <param name="record">The composite record.</param>
    /// <param name="offset">The read position, after the flags and glyph index.</param>
    /// <param name="flags">The component flags.</param>
    /// <returns>The component transform.</returns>
    private static FontMatrix ReadComponentTransform(ReadOnlySpan<byte> record, ref int offset, int flags)
    {
        var xy = (flags & ArgsAreXYValues) != 0;
        float dx;
        float dy;
        if ((flags & ArgsAreWords) != 0)
        {
            dx = FontBytes.S16(record, offset);
            dy = FontBytes.S16(record, offset + FontBytes.U16Size);
            offset += FontBytes.U32Size;
        }
        else
        {
            dx = FontBytes.S8(record, offset);
            dy = FontBytes.S8(record, offset + 1);
            offset += FontBytes.U16Size;
        }

        var matrix = ReadComponentMatrix(record, ref offset, flags);
        if (!xy)
        {
            return matrix;
        }

        var scaled = (flags & ScaledOffset) != 0 && (flags & UnscaledOffset) == 0;
        return scaled
            ? matrix with { E = matrix.TransformX(dx, dy), F = matrix.TransformY(dx, dy) }
            : matrix with { E = dx, F = dy };
    }

    /// <summary>Reads a component's scale or 2x2 matrix.</summary>
    /// <param name="record">The composite record.</param>
    /// <param name="offset">The read position.</param>
    /// <param name="flags">The component flags.</param>
    /// <returns>The matrix without an offset.</returns>
    private static FontMatrix ReadComponentMatrix(ReadOnlySpan<byte> record, ref int offset, int flags)
    {
        FontMatrix matrix;
        if ((flags & HasScale) != 0)
        {
            matrix = FontMatrix.FromScale(FontBytes.F2Dot14(record, offset));
            offset += F2Dot14Size;
        }
        else if ((flags & HasXYScale) != 0)
        {
            matrix = new(FontBytes.F2Dot14(record, offset), 0, 0, FontBytes.F2Dot14(record, offset + F2Dot14Size), 0, 0);
            offset += XYScaleSize;
        }
        else if ((flags & HasTwoByTwo) != 0)
        {
            matrix = new(
                FontBytes.F2Dot14(record, offset),
                FontBytes.F2Dot14(record, offset + F2Dot14Size),
                FontBytes.F2Dot14(record, offset + (F2Dot14Size * ThirdValue)),
                FontBytes.F2Dot14(record, offset + (F2Dot14Size * FourthValue)),
                0,
                0);
            offset += TwoByTwoSize;
        }
        else
        {
            matrix = FontMatrix.Identity;
        }

        return matrix;
    }

    /// <summary>Gets a glyph's record from 'glyf'.</summary>
    /// <param name="data">The font data.</param>
    /// <param name="glyph">The glyph id.</param>
    /// <returns>The record, or empty for an empty or invalid glyph.</returns>
    private ReadOnlySpan<byte> GetGlyphRecord(ReadOnlySpan<byte> data, int glyph)
    {
        if ((uint)glyph >= (uint)GlyphCount)
        {
            return [];
        }

        var loca = _loca.Of(data);
        long start;
        long end;
        if (_longLoca)
        {
            start = FontBytes.U32(loca, glyph * LongLocaSize);
            end = FontBytes.U32(loca, (glyph + 1) * LongLocaSize);
        }
        else
        {
            start = (long)FontBytes.U16(loca, glyph * FontBytes.U16Size) * ShortLocaScale;
            end = (long)FontBytes.U16(loca, (glyph + 1) * FontBytes.U16Size) * ShortLocaScale;
        }

        var length = end - start;
        return length >= GlyfOutline.HeaderSize && end <= _glyf.Length ? _glyf.Of(data).Slice((int)start, (int)length) : [];
    }

    /// <summary>Decodes a 'glyf' glyph, simple or composite.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="glyph">The glyph id.</param>
    /// <param name="transform">The transform to apply.</param>
    /// <param name="depth">The composite nesting depth.</param>
    /// <param name="sink">The sink.</param>
    private void DecodeGlyf<TSink>(int glyph, in FontMatrix transform, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var record = GetGlyphRecord(_data.Span, glyph);
        if (record.IsEmpty)
        {
            return;
        }

        var contours = FontBytes.S16(record, 0);
        if (contours >= 0)
        {
            GlyfOutline.DecodeSimple(record, contours, transform, ref sink);
        }
        else if (depth < MaxCompositeDepth)
        {
            DecodeComposite(record, transform, depth, ref sink);
        }
    }

    /// <summary>Decodes each component of a composite glyph with its transform.</summary>
    /// <typeparam name="TSink">The sink type.</typeparam>
    /// <param name="record">The composite record.</param>
    /// <param name="transform">The transform to apply.</param>
    /// <param name="depth">The composite nesting depth.</param>
    /// <param name="sink">The sink.</param>
    private void DecodeComposite<TSink>(ReadOnlySpan<byte> record, in FontMatrix transform, int depth, ref TSink sink)
        where TSink : IGlyphOutlineSink, allows ref struct
    {
        var offset = GlyfOutline.HeaderSize;
        int flags;
        do
        {
            if (offset + ComponentHeader > record.Length)
            {
                return;
            }

            flags = FontBytes.U16(record, offset);
            var child = FontBytes.U16(record, offset + FontBytes.U16Size);
            offset += ComponentHeader;
            var local = ReadComponentTransform(record, ref offset, flags);
            DecodeGlyf(child, transform.Multiply(local), depth + 1, ref sink);
        }
        while ((flags & MoreComponents) != 0);
    }
}
