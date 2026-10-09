// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Writes <c>/ToUnicode</c> CMaps for fonts written with two-byte codes, so text in an embedded font can be selected,
/// searched, copied and read aloud.
/// </summary>
public static class PdfToUnicodeMaps
{
    /// <summary>The most entries a <c>beginbfchar</c> block may hold.</summary>
    private const int BlockEntries = 100;

    /// <summary>The hexadecimal digits of a two-byte code.</summary>
    private const int CodeDigits = 4;

    /// <summary>Gets the CMap's opening.</summary>
    private static ReadOnlySpan<byte> Header => """
        /CIDInit /ProcSet findresource begin
        12 dict begin
        begincmap
        /CIDSystemInfo << /Registry (Adobe) /Ordering (UCS) /Supplement 0 >> def
        /CMapName /Adobe-Identity-UCS def
        /CMapType 2 def
        1 begincodespacerange
        <0000> <FFFF>
        endcodespacerange

        """u8;

    /// <summary>Gets the CMap's closing.</summary>
    private static ReadOnlySpan<byte> Footer => "endcmap\nCMapName currentdict /CMap defineresource pop\nend\nend\n"u8;

    /// <summary>Writes a map from two-byte codes to the text each shows.</summary>
    /// <param name="texts">The text of each code, by code; codes with no text are left out.</param>
    /// <returns>The CMap program.</returns>
    public static byte[] Write(ReadOnlySpan<string?> texts)
    {
        var output = default(PooledBuffer);
        try
        {
            output.Write(Header);
            var code = 0;
            while (code < texts.Length)
            {
                code = WriteBlock(texts, code, ref output);
            }

            output.Write(Footer);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Writes one block of up to <see cref="BlockEntries"/> entries.</summary>
    /// <param name="texts">The texts.</param>
    /// <param name="start">The first code to consider.</param>
    /// <param name="output">The buffer.</param>
    /// <returns>The code after the last one considered.</returns>
    private static int WriteBlock(ReadOnlySpan<string?> texts, int start, ref PooledBuffer output)
    {
        var count = 0;
        var end = start;
        while (end < texts.Length && count < BlockEntries)
        {
            count += string.IsNullOrEmpty(texts[end]) ? 0 : 1;
            end++;
        }

        if (count == 0)
        {
            return end;
        }

        WriteNumber(count, ref output);
        output.Write(" beginbfchar\n"u8);
        for (var code = start; code < end; code++)
        {
            if (texts[code] is { Length: > 0 } text)
            {
                WriteEntry(code, text, ref output);
            }
        }

        output.Write("endbfchar\n"u8);
        return end;
    }

    /// <summary>Writes one code and its UTF-16 text.</summary>
    /// <param name="code">The code.</param>
    /// <param name="text">The text.</param>
    /// <param name="output">The buffer.</param>
    private static void WriteEntry(int code, string text, ref PooledBuffer output)
    {
        output.WriteByte((byte)'<');
        WriteHex(code, ref output);
        output.Write("> <"u8);
        foreach (var c in text)
        {
            WriteHex(c, ref output);
        }

        output.Write(">\n"u8);
    }

    /// <summary>Writes a 16-bit value as four upper case hexadecimal digits.</summary>
    /// <param name="value">The value.</param>
    /// <param name="output">The buffer.</param>
    private static void WriteHex(int value, ref PooledBuffer output)
    {
        _ = Utf8Formatter.TryFormat((ushort)value, output.GetSpan(CodeDigits), out var written, new('X', CodeDigits));
        output.Advance(written);
    }

    /// <summary>Writes a decimal number.</summary>
    /// <param name="value">The number.</param>
    /// <param name="output">The buffer.</param>
    private static void WriteNumber(int value, ref PooledBuffer output)
    {
        _ = Utf8Formatter.TryFormat(value, output.GetSpan(CodeDigits), out var written);
        output.Advance(written);
    }
}
