// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Makes names unique when carried structures meet the target's, keeping the encoding of the name they rename.</summary>
internal static class PdfCarryText
{
    /// <summary>The first byte of the UTF-16BE byte order mark.</summary>
    private const byte Utf16BeFirst = 0xFE;

    /// <summary>The second byte of the UTF-16BE byte order mark.</summary>
    private const byte Utf16BeSecond = 0xFF;

    /// <summary>The bytes of a UTF-16BE character.</summary>
    private const int Utf16Width = 2;

    /// <summary>Gets the text a name stands for, used to compare names whatever their encoding.</summary>
    /// <param name="bytes">The string's bytes.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Key(ReadOnlySpan<byte> bytes) => PdfText.Decode(bytes);

    /// <summary>Picks a name that is not used yet, adding it to the used names.</summary>
    /// <param name="name">The wanted name's bytes.</param>
    /// <param name="used">The names in use, by <see cref="Key"/>.</param>
    /// <returns>The wanted name when free, otherwise it with <c>_1</c>, <c>_2</c> and so on added.</returns>
    internal static byte[] Unique(byte[] name, HashSet<string> used)
    {
        if (used.Add(Key(name)))
        {
            return name;
        }

        var number = 1;
        var candidate = WithSuffix(name, number);
        while (!used.Add(Key(candidate)))
        {
            number++;
            candidate = WithSuffix(name, number);
        }

        return candidate;
    }

    /// <summary>Adds <c>_</c> and a number to a string, in the string's own encoding.</summary>
    /// <param name="name">The string's bytes.</param>
    /// <param name="number">The number.</param>
    /// <returns>The new bytes.</returns>
    internal static byte[] WithSuffix(ReadOnlySpan<byte> name, int number)
    {
        var suffix = string.Create(CultureInfo.InvariantCulture, $"_{number}");
        var wide = name.Length >= Utf16Width && name[0] == Utf16BeFirst && name[1] == Utf16BeSecond;
        var result = new byte[name.Length + (suffix.Length * (wide ? Utf16Width : 1))];
        name.CopyTo(result);
        var offset = name.Length;
        foreach (var character in suffix)
        {
            if (wide)
            {
                offset++;
            }

            result[offset] = (byte)character;
            offset++;
        }

        return result;
    }
}
