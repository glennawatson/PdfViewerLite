// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text.Unicode;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// Parses and formats PDF dates, <c>D:YYYYMMDDHHmmSSOHH'mm'</c>, where every part after the year is optional. Works on
/// the string's bytes, so nothing is decoded to text first.
/// </summary>
public static class PdfDate
{
    /// <summary>The length of the year field.</summary>
    private const int YearLength = 4;

    /// <summary>The length of every other field.</summary>
    private const int FieldLength = 2;

    /// <summary>The longest formatted date.</summary>
    private const int FormattedLength = 32;

    /// <summary>Gets the longest <see cref="Format"/> writes.</summary>
    public static int MaxFormattedLength => FormattedLength;

    /// <summary>Gets the date prefix.</summary>
    private static ReadOnlySpan<byte> Prefix => "D:"u8;

    /// <summary>Parses a PDF date.</summary>
    /// <param name="value">The string's bytes.</param>
    /// <returns>The date, or <see langword="null"/> when it cannot be parsed.</returns>
    public static DateTimeOffset? Parse(ReadOnlySpan<byte> value)
    {
        var span = value.Trim(" \t\r\n"u8);
        if (span.StartsWith(Prefix))
        {
            span = span[Prefix.Length..];
        }

        var position = 0;
        if (!TryReadField(span, ref position, YearLength, out var year))
        {
            return null;
        }

        var month = ReadOptional(span, ref position, 1);
        var day = ReadOptional(span, ref position, 1);
        var hour = ReadOptional(span, ref position, 0);
        var minute = ReadOptional(span, ref position, 0);
        var second = ReadOptional(span, ref position, 0);
        var offset = ReadOffset(span[position..]);
        try
        {
            return new DateTimeOffset(year, month, day, hour, minute, second, offset);
        }
        catch (ArgumentOutOfRangeException)
        {
            return null;
        }
    }

    /// <summary>Formats a date as <c>D:YYYYMMDDHHmmSS+HH'mm'</c>.</summary>
    /// <param name="value">The date.</param>
    /// <param name="destination">The buffer, at least <see cref="MaxFormattedLength"/> bytes.</param>
    /// <returns>The number of bytes written.</returns>
    public static int Format(DateTimeOffset value, Span<byte> destination)
    {
        var offset = value.Offset;
        var sign = offset < TimeSpan.Zero ? '-' : '+';
        var absolute = offset.Duration();
        _ = Utf8.TryWrite(
            destination,
            CultureInfo.InvariantCulture,
            $"D:{value:yyyyMMddHHmmss}{sign}{absolute.Hours:00}'{absolute.Minutes:00}'",
            out var written);
        return written;
    }

    /// <summary>Reads an optional two digit field.</summary>
    /// <param name="span">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="fallback">The value when the field is absent.</param>
    /// <returns>The value.</returns>
    private static int ReadOptional(ReadOnlySpan<byte> span, ref int position, int fallback) =>
        TryReadField(span, ref position, FieldLength, out var value) ? value : fallback;

    /// <summary>Reads a fixed width number.</summary>
    /// <param name="span">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="length">The field width.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when read.</returns>
    private static bool TryReadField(ReadOnlySpan<byte> span, ref int position, int length, out int value)
    {
        if (position + length <= span.Length && int.TryParse(span.Slice(position, length), NumberStyles.None, CultureInfo.InvariantCulture, out value))
        {
            position += length;
            return true;
        }

        value = 0;
        return false;
    }

    /// <summary>Reads the time zone suffix (<c>Z</c>, <c>+HH'mm'</c> or <c>-HH'mm'</c>).</summary>
    /// <param name="span">The remaining text.</param>
    /// <returns>The offset.</returns>
    private static TimeSpan ReadOffset(ReadOnlySpan<byte> span)
    {
        if (span.IsEmpty || (span[0] != '+' && span[0] != '-'))
        {
            return TimeSpan.Zero;
        }

        var sign = span[0] == '-' ? -1 : 1;
        var position = 1;
        var hours = ReadOptional(span, ref position, 0);
        if (position < span.Length && span[position] == '\'')
        {
            position++;
        }

        var minutes = ReadOptional(span, ref position, 0);
        return sign * new TimeSpan(hours, minutes, 0);
    }
}
