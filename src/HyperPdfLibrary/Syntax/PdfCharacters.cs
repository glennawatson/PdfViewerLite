// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Syntax;

/// <summary>PDF character classes as vectorised search sets.</summary>
internal static class PdfCharacters
{
    /// <summary>The value of a hexadecimal digit that is not one.</summary>
    internal const int NotHex = -1;

    /// <summary>The white-space characters.</summary>
    internal static readonly SearchValues<byte> Whitespace = SearchValues.Create(" \t\r\n\f\0"u8);

    /// <summary>The characters that end a regular token: white space and delimiters.</summary>
    internal static readonly SearchValues<byte> TokenEnd = SearchValues.Create(" \t\r\n\f\0()<>[]{}/%"u8);

    /// <summary>The characters that matter inside a literal string.</summary>
    internal static readonly SearchValues<byte> LiteralStringSpecials = SearchValues.Create("()\\"u8);

    /// <summary>The line-end characters.</summary>
    internal static readonly SearchValues<byte> LineEnd = SearchValues.Create("\r\n"u8);

    /// <summary>The hexadecimal digits.</summary>
    internal static readonly SearchValues<byte> HexDigits = SearchValues.Create("0123456789abcdefABCDEF"u8);

    /// <summary>Determines whether a byte is white space.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when white space.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool IsWhitespace(byte value) => Whitespace.Contains(value);

    /// <summary>Determines whether a byte ends a regular token.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> when white space or a delimiter.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool EndsToken(byte value) => TokenEnd.Contains(value);

    /// <summary>Gets the value of a hexadecimal digit.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The value, or <see cref="NotHex"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HexValue(byte value)
    {
        const int LetterBase = 10;
        return value switch
        {
            >= (byte)'0' and <= (byte)'9' => value - '0',
            >= (byte)'a' and <= (byte)'f' => value - 'a' + LetterBase,
            >= (byte)'A' and <= (byte)'F' => value - 'A' + LetterBase,
            _ => NotHex,
        };
    }
}
