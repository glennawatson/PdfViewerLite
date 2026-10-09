// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Reads a backward-growing HT bit-stream (T.814): the VLC stream at the end of a cleanup
/// segment and the MagRef stream at the end of a refinement segment. Bytes are read from the last one down, bits least
/// significant first. After a byte above 0x8F, a byte whose low seven bits are all ones holds only those seven bits.
/// Past the start it reads zeros.
/// </summary>
internal ref struct JpxHtReverseReader
{
    /// <summary>The bits kept ready before a peek.</summary>
    private const int PeekBits = 32;

    /// <summary>The most bits held before a refill stops: room for one more byte.</summary>
    private const int RefillLimit = 56;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The bytes at the end of a cleanup segment that hold Scup: the last byte and a nibble of the one before.</summary>
    private const int SuffixLengthBytes = 2;

    /// <summary>The seven low bits of a byte.</summary>
    private const uint SevenBits = 0x7F;

    /// <summary>The largest byte after which no bit is stuffed.</summary>
    private const uint StuffLimit = 0x8F;

    /// <summary>The low nibble of a byte.</summary>
    private const uint LowNibble = 0x0F;

    /// <summary>The three low bits of the first nibble that, all set, mark its top bit as stuffed.</summary>
    private const uint NibbleStuffMask = 0x07;

    /// <summary>The bytes of the stream, read from the end down.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The position of the next byte, or -1 past the start.</summary>
    private int _position;

    /// <summary>The bits read but not consumed, the next bit lowest.</summary>
    private ulong _bits;

    /// <summary>The number of bits in <see cref="_bits"/>.</summary>
    private int _count;

    /// <summary>Whether the last byte read was above 0x8F.</summary>
    private bool _afterLarge;

    /// <summary>Initializes a new instance of the <see cref="JpxHtReverseReader"/> struct.</summary>
    /// <param name="data">The stream's bytes; reading starts at the last one.</param>
    /// <param name="afterLarge">Whether the byte before the last counts as above 0x8F.</param>
    private JpxHtReverseReader(ReadOnlySpan<byte> data, bool afterLarge)
    {
        _data = data;
        _position = data.Length - 1;
        _afterLarge = afterLarge;
    }

    /// <summary>Starts the VLC stream of a cleanup segment, whose first bits are the top nibble of the segment's second-last byte.</summary>
    /// <param name="segment">The cleanup segment.</param>
    /// <param name="suffixLength">The bytes of MEL and VLC data, Scup.</param>
    /// <returns>The reader.</returns>
    internal static JpxHtReverseReader ForVlc(ReadOnlySpan<byte> segment, int suffixLength)
    {
        // The last byte and the low nibble of the one before hold Scup; the stream itself runs from that nibble back.
        var start = segment.Length - suffixLength;
        var first = (uint)segment[^SuffixLengthBytes];
        var reader = new JpxHtReverseReader(segment.Slice(start, suffixLength - SuffixLengthBytes), (first | LowNibble) > StuffLimit);
        var nibble = first >> NibbleBits;
        if ((nibble & NibbleStuffMask) == NibbleStuffMask)
        {
            reader._bits = nibble & NibbleStuffMask;
            reader._count = NibbleBits - 1;
        }
        else
        {
            reader._bits = nibble;
            reader._count = NibbleBits;
        }

        reader.Refill();
        return reader;
    }

    /// <summary>Starts the MagRef stream of a refinement segment, read from its last byte.</summary>
    /// <param name="segment">The refinement segment.</param>
    /// <returns>The reader.</returns>
    internal static JpxHtReverseReader ForMagRef(ReadOnlySpan<byte> segment)
    {
        var reader = new JpxHtReverseReader(segment, true);
        reader.Refill();
        return reader;
    }

    /// <summary>Gets the next 32 bits without consuming them, the next bit lowest.</summary>
    /// <returns>The bits.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint Peek()
    {
        if (_count < PeekBits)
        {
            Refill();
        }

        return (uint)_bits;
    }

    /// <summary>Consumes bits returned by <see cref="Peek"/>.</summary>
    /// <param name="count">The bits, at most 32.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Skip(int count)
    {
        _bits >>= count;
        _count -= count;
    }

    /// <summary>Adds whole bytes until the bit store is nearly full.</summary>
    private void Refill()
    {
        while (_count <= RefillLimit)
        {
            var value = 0U;
            if (_position >= 0)
            {
                value = _data[_position];
                _position--;
            }

            if (_afterLarge && (value & SevenBits) == SevenBits)
            {
                value &= SevenBits;
                _bits |= (ulong)value << _count;
                _count += ByteBits - 1;
            }
            else
            {
                _bits |= (ulong)value << _count;
                _count += ByteBits;
            }

            _afterLarge = value > StuffLimit;
        }
    }
}
