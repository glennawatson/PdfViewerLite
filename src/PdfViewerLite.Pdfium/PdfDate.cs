// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace PdfViewerLite.Pdfium;

/// <summary>Parses PDF date strings of the form <c>D:YYYYMMDDHHmmSSOHH'mm'</c>, where every part after the year is optional.</summary>
internal static class PdfDate
{
    /// <summary>The length of the year field.</summary>
    private const int YearLength = 4;

    /// <summary>The length of every other two digit field.</summary>
    private const int FieldLength = 2;

    /// <summary>Parses a PDF date.</summary>
    /// <param name="value">The raw value.</param>
    /// <returns>The date, or <see langword="null"/> when the value cannot be parsed.</returns>
    internal static DateTimeOffset? Parse(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var span = value.AsSpan().Trim();
        if (span.StartsWith("D:", StringComparison.Ordinal))
        {
            span = span[FieldLength..];
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

    /// <summary>Reads an optional two digit field.</summary>
    /// <param name="span">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="fallback">The value when the field is absent.</param>
    /// <returns>The value.</returns>
    private static int ReadOptional(ReadOnlySpan<char> span, ref int position, int fallback) =>
        TryReadField(span, ref position, FieldLength, out var value) ? value : fallback;

    /// <summary>Reads a fixed width number.</summary>
    /// <param name="span">The text.</param>
    /// <param name="position">The read position.</param>
    /// <param name="length">The field width.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when read.</returns>
    private static bool TryReadField(ReadOnlySpan<char> span, ref int position, int length, out int value)
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
    private static TimeSpan ReadOffset(ReadOnlySpan<char> span)
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
