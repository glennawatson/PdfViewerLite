// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfViewerLite.Core.Forms.Scripting;

/// <summary>
/// Reads and writes dates in Acrobat's form date formats, such as <c>dd/mm/yyyy</c> or <c>mmm d, yyyy</c>: in them
/// <c>m</c> is the month and <c>M</c> the minute, the reverse of .NET's patterns.
/// </summary>
internal static class FormDates
{
    /// <summary>The letters that mean the same in Acrobat's formats and .NET's patterns.</summary>
    private static readonly SearchValues<char> Kept = SearchValues.Create("dyHhst");

    /// <summary>The characters .NET's patterns treat specially, escaped to be written as they are.</summary>
    private static readonly SearchValues<char> Escaped = SearchValues.Create("\\'\"%");

    /// <summary>Common date forms tried when the text is not in the field's format.</summary>
    private static readonly string[] CommonPatterns = ["yyyy-MM-dd", "yyyy-MM-ddTHH:mm:ss", "d MMM yyyy", "d MMMM yyyy", "MMM d, yyyy", "MMMM d, yyyy"];

    /// <summary>Converts an Acrobat date format to a .NET pattern.</summary>
    /// <param name="format">The Acrobat format.</param>
    /// <returns>The .NET pattern.</returns>
    internal static string ToPattern(string format)
    {
        ArgumentNullException.ThrowIfNull(format);
        var pattern = new StringBuilder(format.Length + format.Length);
        foreach (var c in format)
        {
            if (c is 'm' or 'M')
            {
                // Acrobat's month and minute letters are the other way round to .NET's.
                _ = pattern.Append(c == 'm' ? 'M' : 'm');
            }
            else if (Kept.Contains(c))
            {
                _ = pattern.Append(c);
            }
            else
            {
                _ = Escaped.Contains(c) || char.IsAsciiLetter(c) ? pattern.Append('\\').Append(c) : pattern.Append(c);
            }
        }

        return pattern.ToString();
    }

    /// <summary>Reads a date written in the field's format, or failing that in a common form.</summary>
    /// <param name="text">The text.</param>
    /// <param name="format">The field's Acrobat format.</param>
    /// <param name="value">The date.</param>
    /// <returns><see langword="true"/> when the text is a date.</returns>
    internal static bool TryParse(string? text, string format, out DateTime value)
    {
        value = default;
        if (string.IsNullOrWhiteSpace(text))
        {
            return false;
        }

        var trimmed = text.Trim();
        return DateTime.TryParseExact(trimmed, ToPattern(format), CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParseExact(trimmed, CommonPatterns, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out value)
            || DateTime.TryParse(trimmed, CultureInfo.CurrentCulture, DateTimeStyles.AllowWhiteSpaces, out value);
    }

    /// <summary>Writes a date in an Acrobat format.</summary>
    /// <param name="value">The date.</param>
    /// <param name="format">The Acrobat format.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string Format(DateTime value, string format) => value.ToString(ToPattern(format), CultureInfo.InvariantCulture);
}
