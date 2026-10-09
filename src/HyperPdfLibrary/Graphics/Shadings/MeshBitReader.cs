// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>Reads big-endian bit fields of up to 32 bits from mesh shading data.</summary>
internal ref struct MeshBitReader
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The most bits one read returns.</summary>
    private const int MaxBits = 32;

    /// <summary>The data.</summary>
    private readonly ReadOnlySpan<byte> _data;

    /// <summary>The position of the next bit.</summary>
    private long _position;

    /// <summary>Initializes a new instance of the <see cref="MeshBitReader"/> struct.</summary>
    /// <param name="data">The data.</param>
    internal MeshBitReader(ReadOnlySpan<byte> data) => _data = data;

    /// <summary>Gets the bits left.</summary>
    internal readonly long Remaining => ((long)_data.Length * ByteBits) - _position;

    /// <summary>Reads a field.</summary>
    /// <param name="bits">The width, from 0 to 32.</param>
    /// <returns>The value; bits past the end read as zero.</returns>
    internal uint Read(int bits)
    {
        if (bits is <= 0 or > MaxBits)
        {
            return 0;
        }

        ulong value = 0;
        for (var i = 0; i < bits; i++)
        {
            var index = (int)(_position >> 3);
            var bit = index < _data.Length ? (_data[index] >> (ByteBits - 1 - (int)(_position & (ByteBits - 1)))) & 1 : 0;
            value = (value << 1) | (uint)bit;
            _position++;
        }

        return (uint)value;
    }

    /// <summary>Skips to the next byte boundary.</summary>
    internal void Align() => _position = (_position + ByteBits - 1) / ByteBits * ByteBits;
}
