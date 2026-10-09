// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;

namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Reads the bounded variable-width numbers of a binary CMap resource.</summary>
internal ref struct BinaryCMapStream
{
    /// <summary>The maximum encoded number length for a sixteen-byte code.</summary>
    private const int MaxNumberBytes = 19;

    /// <summary>The bits carried by each encoded byte.</summary>
    private const int GroupBits = 7;

    /// <summary>The flag marking another encoded byte.</summary>
    private const byte MoreFlag = 0x80;

    /// <summary>The value bits of an encoded byte.</summary>
    private const byte ValueMask = 0x7F;

    /// <summary>The longest metadata string accepted.</summary>
    private const int MaxStringLength = 4096;

    /// <summary>The remaining resource bytes.</summary>
    private ReadOnlySpan<byte> _data;

    /// <summary>Initializes a new instance of the <see cref="BinaryCMapStream"/> struct.</summary>
    /// <param name="data">The binary resource.</param>
    internal BinaryCMapStream(ReadOnlySpan<byte> data) => _data = data;

    /// <summary>Gets a value indicating whether every byte was read.</summary>
    internal readonly bool AtEnd => _data.IsEmpty;

    /// <summary>Reads one byte.</summary>
    /// <returns>The byte.</returns>
    /// <exception cref="InvalidDataException">The resource ends early.</exception>
    internal byte ReadByte()
    {
        if (_data.IsEmpty)
        {
            throw new InvalidDataException("The binary CMap ended early.");
        }

        var value = _data[0];
        _data = _data[1..];
        return value;
    }

    /// <summary>Reads a big-endian base-128 number.</summary>
    /// <returns>The number.</returns>
    /// <exception cref="InvalidDataException">The encoded number is too long.</exception>
    internal BigInteger ReadNumber()
    {
        var value = BigInteger.Zero;
        for (var index = 0; index < MaxNumberBytes; index++)
        {
            var next = ReadByte();
            value = (value << GroupBits) | (next & ValueMask);
            if ((next & MoreFlag) == 0)
            {
                return value;
            }
        }

        throw new InvalidDataException("The binary CMap number is too long.");
    }

    /// <summary>Reads a zigzag signed number.</summary>
    /// <returns>The signed value.</returns>
    internal BigInteger ReadSigned()
    {
        var value = ReadNumber();
        return value.IsEven ? value >> 1 : ~(value >> 1);
    }

    /// <summary>Reads a fixed-width unsigned value.</summary>
    /// <param name="length">The number of bytes.</param>
    /// <returns>The value.</returns>
    /// <exception cref="InvalidDataException">The resource ends early.</exception>
    internal BigInteger ReadHex(int length)
    {
        if (length > _data.Length)
        {
            throw new InvalidDataException("The binary CMap value ended early.");
        }

        var value = new BigInteger(_data[..length], isUnsigned: true, isBigEndian: true);
        _data = _data[length..];
        return value;
    }

    /// <summary>Reads a metadata string.</summary>
    /// <returns>The text.</returns>
    /// <exception cref="InvalidDataException">The metadata string is too long.</exception>
    internal string ReadString()
    {
        var length = (int)ReadNumber();
        if (length > MaxStringLength)
        {
            throw new InvalidDataException("The binary CMap metadata is too long.");
        }

        var characters = new char[length];
        for (var index = 0; index < length; index++)
        {
            characters[index] = (char)ReadNumber();
        }

        return new(characters);
    }
}
