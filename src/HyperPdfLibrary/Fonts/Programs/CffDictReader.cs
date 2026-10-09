// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>Reads the operator entries of a CFF DICT, collecting each operator's operands in caller-provided space.</summary>
/// <param name="dict">The DICT data.</param>
/// <param name="operands">Space for the operands, usually <see cref="CharstringLimits.StackDepth"/> values on the stack.</param>
internal ref struct CffDictReader(ReadOnlySpan<byte> dict, Span<double> operands)
{
    /// <summary>The escape byte of two-byte operators.</summary>
    internal const int Escape = 12;

    /// <summary>The shift that marks a two-byte operator.</summary>
    internal const int EscapeShift = 8;

    /// <summary>The largest one-byte operator.</summary>
    private const int MaxOperator = 21;

    /// <summary>The prefix of a 16-bit integer.</summary>
    private const int ShortInt = 28;

    /// <summary>The prefix of a 32-bit integer.</summary>
    private const int LongInt = 29;

    /// <summary>The prefix of a real number.</summary>
    private const int Real = 30;

    /// <summary>The first single-byte integer.</summary>
    private const int SmallIntStart = 32;

    /// <summary>The last single-byte integer.</summary>
    private const int SmallIntEnd = 246;

    /// <summary>The bias of single-byte integers.</summary>
    private const int SmallIntBias = 139;

    /// <summary>The first positive two-byte integer prefix.</summary>
    private const int PositiveStart = 247;

    /// <summary>The first negative two-byte integer prefix.</summary>
    private const int NegativeStart = 251;

    /// <summary>The last integer prefix.</summary>
    private const int LastIntPrefix = 254;

    /// <summary>The bias of two-byte integers.</summary>
    private const int TwoByteBias = 108;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The longest real number text.</summary>
    private const int MaxRealText = 64;

    /// <summary>The most text one nibble adds.</summary>
    private const int MaxNibbleText = 2;

    /// <summary>The nibble for a decimal point.</summary>
    private const int NibblePoint = 0xA;

    /// <summary>The nibble for a positive exponent.</summary>
    private const int NibbleExponent = 0xB;

    /// <summary>The nibble for a negative exponent.</summary>
    private const int NibbleNegativeExponent = 0xC;

    /// <summary>The nibble for a minus sign.</summary>
    private const int NibbleMinus = 0xE;

    /// <summary>The nibble that ends the number.</summary>
    private const int NibbleEnd = 0xF;

    /// <summary>The bits in a nibble.</summary>
    private const int NibbleBits = 4;

    /// <summary>The mask of a nibble.</summary>
    private const int NibbleMask = 0xF;

    /// <summary>The DICT data.</summary>
    private readonly ReadOnlySpan<byte> _dict = dict;

    /// <summary>The operands of the current entry.</summary>
    private readonly Span<double> _operands = operands;

    /// <summary>The read position.</summary>
    private int _position;

    /// <summary>Gets the number of operands of the current entry.</summary>
    internal int Count { get; private set; }

    /// <summary>Reads the next entry.</summary>
    /// <param name="op">The operator; two-byte operators are <c>(12 &lt;&lt; 8) | b1</c>.</param>
    /// <returns><see langword="false"/> at the end of the DICT.</returns>
    internal bool TryRead(out int op)
    {
        Count = 0;
        while (_position < _dict.Length)
        {
            int b = _dict[_position];
            if (b <= MaxOperator)
            {
                _position++;
                op = b;
                if (b == Escape)
                {
                    op = (Escape << EscapeShift) | FontBytes.U8(_dict, _position);
                    _position++;
                }

                return true;
            }

            ReadOperand(b);
        }

        op = 0;
        return false;
    }

    /// <summary>Gets an operand.</summary>
    /// <param name="index">The operand index.</param>
    /// <returns>The operand, or zero when missing.</returns>
    internal readonly double Get(int index) => (uint)index < (uint)Count ? _operands[index] : 0;

    /// <summary>Gets an operand as an integer.</summary>
    /// <param name="index">The operand index.</param>
    /// <returns>The operand, or zero when missing.</returns>
    internal readonly int GetInt(int index) => (int)Math.Clamp(Get(index), int.MinValue, int.MaxValue);

    /// <summary>Appends the text of one nibble.</summary>
    /// <param name="nibble">The nibble.</param>
    /// <param name="text">The text buffer.</param>
    /// <param name="length">The text length.</param>
    /// <returns><see langword="true"/> when the nibble ends the number.</returns>
    private static bool AppendNibble(int nibble, Span<byte> text, ref int length)
    {
        if (nibble == NibbleEnd || length + MaxNibbleText > text.Length)
        {
            return true;
        }

        if (nibble < NibblePoint)
        {
            text[length] = (byte)('0' + nibble);
            length++;
            return false;
        }

        var piece = nibble switch
        {
            NibblePoint => "."u8,
            NibbleExponent => "E"u8,
            NibbleNegativeExponent => "E-"u8,
            NibbleMinus => "-"u8,
            _ => [],
        };

        piece.CopyTo(text[length..]);
        length += piece.Length;
        return false;
    }

    /// <summary>Reads one operand.</summary>
    /// <param name="b">The first byte.</param>
    private void ReadOperand(int b)
    {
        double value;
        switch (b)
        {
            case ShortInt:
                {
                    value = FontBytes.S16(_dict, _position + 1);
                    _position += 1 + FontBytes.U16Size;
                    break;
                }

            case LongInt:
                {
                    value = (int)FontBytes.U32(_dict, _position + 1);
                    _position += 1 + FontBytes.U32Size;
                    break;
                }

            case Real:
                {
                    value = ReadReal();
                    break;
                }

            default:
                {
                    value = ReadInteger(b);
                    break;
                }
        }

        if (Count >= _operands.Length)
        {
            return;
        }

        _operands[Count] = value;
        Count++;
    }

    /// <summary>Reads a one- or two-byte integer; reserved bytes read as zero.</summary>
    /// <param name="b">The first byte.</param>
    /// <returns>The value.</returns>
    private int ReadInteger(int b)
    {
        _position++;
        if (b is >= SmallIntStart and <= SmallIntEnd)
        {
            return b - SmallIntBias;
        }

        if (b is < PositiveStart or > LastIntPrefix)
        {
            return 0;
        }

        var next = FontBytes.U8(_dict, _position);
        _position++;
        return b < NegativeStart
            ? ((b - PositiveStart) << ByteBits) + next + TwoByteBias
            : -((b - NegativeStart) << ByteBits) - next - TwoByteBias;
    }

    /// <summary>Reads a nibble-encoded real number.</summary>
    /// <returns>The value, or zero when malformed.</returns>
    private double ReadReal()
    {
        _position++;
        Span<byte> text = stackalloc byte[MaxRealText];
        var length = 0;
        var ended = false;
        while (!ended && _position < _dict.Length)
        {
            int b = _dict[_position];
            _position++;
            ended = AppendNibble(b >> NibbleBits, text, ref length) || AppendNibble(b & NibbleMask, text, ref length);
        }

        return double.TryParse(text[..length], NumberStyles.Float, CultureInfo.InvariantCulture, out var value) ? value : 0;
    }
}
