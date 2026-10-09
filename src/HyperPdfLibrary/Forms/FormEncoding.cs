// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Forms;

/// <summary>Encodes field text for the built-in font that appearance streams fall back on: WinAnsiEncoding.</summary>
internal static class FormEncoding
{
    /// <summary>The code of a line feed, which ends a line.</summary>
    internal const byte LineFeed = 10;

    /// <summary>The first printable ASCII code.</summary>
    private const char FirstPrintable = ' ';

    /// <summary>The last printable ASCII code.</summary>
    private const char LastPrintable = '~';

    /// <summary>The code written for a character the encoding cannot show.</summary>
    private const byte Replacement = (byte)'?';

    /// <summary>Encodes one character in WinAnsiEncoding.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The code; a question mark when the encoding cannot show the character.</returns>
    internal static byte EncodeCharacter(char c)
    {
        if (c is >= FirstPrintable and <= LastPrintable)
        {
            return (byte)c;
        }

        if (c is '\n')
        {
            return LineFeed;
        }

        if (c is '\t')
        {
            return (byte)FirstPrintable;
        }

        if (!GlyphList.TryGetName(c, out var name))
        {
            return Replacement;
        }

        var code = FontEncodings.GetCode(FontEncoding.WinAnsi, name);
        return code is >= 0 and <= byte.MaxValue ? (byte)code : Replacement;
    }
}
