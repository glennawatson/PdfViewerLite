// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>
/// Reads a forward-growing HT bit-stream (T.814): the MagSgn stream of a cleanup segment
/// and the SigProp stream of a refinement segment. Bits come least significant first; the byte after 0xFF holds only
/// seven bits, its top bit being a stuffed zero. Past the end it reads a fill byte: 0xFF for MagSgn, zero for SigProp.
/// </summary>
internal ref struct JpxHtForwardReader
{
    /// <summary>The bits kept ready before a peek.</summary>
    private const int PeekBits = 32;

    /// <summary>The most bits held before a refill stops: room for one more byte.</summary>
    private const int RefillLimit = 56;

    /// <summary>The bits of a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value of a byte that is all ones.</summary>
    private const uint AllOnes = 0xFF;

    /// <summary>The seven low bits of a byte.</summary>
    private const uint SevenBits = 0x7F;

    /// <summary>The bytes of the stream.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The byte read past the end.</summary>
    private readonly uint _fill;

    /// <summary>The position of the next byte.</summary>
    private int _position;

    /// <summary>The bits read but not consumed, the next bit lowest.</summary>
    private ulong _bits;

    /// <summary>The number of bits in <see cref="_bits"/>.</summary>
    private int _count;

    /// <summary>Whether the next byte's top bit is stuffed.</summary>
    private bool _stuffed;

    /// <summary>Initializes a new instance of the <see cref="JpxHtForwardReader"/> struct.</summary>
    /// <param name="data">The stream's bytes.</param>
    /// <param name="fill">The byte read past the end.</param>
    internal JpxHtForwardReader(ReadOnlySpan<byte> data, byte fill)
    {
        _data = data;
        _fill = fill;
        Refill();
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

    /// <summary>Reads one bit.</summary>
    /// <returns>The bit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal uint ReadBit()
    {
        var bit = Peek() & 1;
        Skip(1);
        return bit;
    }

    /// <summary>Adds whole bytes until the bit store is nearly full.</summary>
    private void Refill()
    {
        while (_count <= RefillLimit)
        {
            var value = _fill;
            if (_position < _data.Length)
            {
                value = _data[_position];
                _position++;
            }

            if (_stuffed)
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

            _stuffed = value == AllOnes;
        }
    }
}
