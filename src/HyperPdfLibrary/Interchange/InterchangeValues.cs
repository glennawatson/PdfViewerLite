// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Xml;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Converts the values XFDF carries as attribute text: numbers, lists, colours, flags and dates.</summary>
internal static class InterchangeValues
{
    /// <summary>The characters one formatted number takes, with its separator.</summary>
    private const int NumberChars = 24;

    /// <summary>The bytes a formatted date can take; at least <see cref="PdfDate.MaxFormattedLength"/>.</summary>
    private const int DateBytes = 32;

    /// <summary>The numbers a rectangle has.</summary>
    private const int RectNumbers = 4;

    /// <summary>The length of a colour written as six hexadecimal digits.</summary>
    private const int ColorDigits = 6;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>The most numbers kept from one list, so a hostile attribute cannot ask for a huge array.</summary>
    private const int MaxListNumbers = 1 << 22;

    /// <summary>The format numbers are written in.</summary>
    private const string NumberFormat = "0.######";

    /// <summary>The flag names XFDF uses, in flag order.</summary>
    private static readonly FlagName[] FlagNames =
    [
        new("invisible", PdfAnnotationFlags.Invisible),
        new("hidden", PdfAnnotationFlags.Hidden),
        new("print", PdfAnnotationFlags.Print),
        new("nozoom", PdfAnnotationFlags.NoZoom),
        new("norotate", PdfAnnotationFlags.NoRotate),
        new("noview", PdfAnnotationFlags.NoView),
        new("readonly", PdfAnnotationFlags.ReadOnly),
        new("locked", PdfAnnotationFlags.Locked),
        new("togglenoview", PdfAnnotationFlags.ToggleNoView),
        new("lockedcontents", PdfAnnotationFlags.LockedContents),
    ];

    /// <summary>The border style names XFDF uses and the PDF letter of each.</summary>
    private static readonly StyleName[] StyleNames =
    [
        new("solid", "S"),
        new("dash", "D"),
        new("beveled", "B"),
        new("inset", "I"),
        new("underline", "U"),
    ];

    /// <summary>Formats a number with up to six decimals.</summary>
    /// <param name="value">The number.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatNumber(float value) => value.ToString(NumberFormat, CultureInfo.InvariantCulture);

    /// <summary>Formats numbers separated by a character.</summary>
    /// <param name="values">The numbers.</param>
    /// <param name="separator">The separator.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatList(ReadOnlySpan<float> values, char separator) => FormatList(values, separator, separator);

    /// <summary>Formats points, <c>x,y;x,y</c>: the numbers alternate between two separators.</summary>
    /// <param name="values">The numbers, two to a point.</param>
    /// <param name="inner">The separator inside a point.</param>
    /// <param name="outer">The separator between points.</param>
    /// <returns>The text.</returns>
    internal static string FormatList(ReadOnlySpan<float> values, char inner, char outer)
    {
        if (values.IsEmpty)
        {
            return string.Empty;
        }

        var rented = ArrayPool<char>.Shared.Rent(values.Length * NumberChars);
        try
        {
            var length = 0;
            for (var i = 0; i < values.Length; i++)
            {
                if (i > 0)
                {
                    rented[length] = (i & 1) == 1 ? inner : outer;
                    length++;
                }

                _ = values[i].TryFormat(rented.AsSpan(length), out var written, NumberFormat, CultureInfo.InvariantCulture);
                length += written;
            }

            return new(rented, 0, length);
        }
        finally
        {
            ArrayPool<char>.Shared.Return(rented);
        }
    }

    /// <summary>Reads numbers separated by commas, semicolons or spaces.</summary>
    /// <param name="text">The text; <see langword="null"/> gives no numbers.</param>
    /// <returns>The numbers; unreadable parts are dropped.</returns>
    internal static float[] ParseList(string? text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return [];
        }

        var span = text.AsSpan();
        var count = 0;
        foreach (var range in span.SplitAny(",; \t\r\n"))
        {
            if (!span[range].IsEmpty && count < MaxListNumbers)
            {
                count++;
            }
        }

        var values = new float[count];
        var at = 0;
        foreach (var range in span.SplitAny(",; \t\r\n"))
        {
            if (at >= count || span[range].IsEmpty)
            {
                continue;
            }

            _ = float.TryParse(span[range], NumberStyles.Float, CultureInfo.InvariantCulture, out values[at]);
            at++;
        }

        return values;
    }

    /// <summary>Reads a rectangle from four numbers.</summary>
    /// <param name="text">The text.</param>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns><see langword="true"/> when the text holds four numbers.</returns>
    internal static bool TryParseRect(string? text, out PdfRectangle rectangle)
    {
        var values = ParseList(text);
        if (values.Length < RectNumbers)
        {
            rectangle = default;
            return false;
        }

        rectangle = PdfRectangle.FromCorners(values[0], values[1], values[2], values[3]);
        return true;
    }

    /// <summary>Formats a rectangle as <c>left,bottom,right,top</c>.</summary>
    /// <param name="rectangle">The rectangle.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatRect(PdfRectangle rectangle) =>
        FormatList([rectangle.Left, rectangle.Bottom, rectangle.Right, rectangle.Top], ',');

    /// <summary>Reads a colour written as <c>#RRGGBB</c>.</summary>
    /// <param name="text">The text.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when the text is a colour.</returns>
    internal static bool TryParseColor(string? text, out uint color)
    {
        color = 0;
        var digits = text.AsSpan().Trim();
        if (digits.StartsWith("#"))
        {
            digits = digits[1..];
        }

        return digits.Length == ColorDigits && uint.TryParse(digits, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out color);
    }

    /// <summary>Formats a colour as <c>#RRGGBB</c>.</summary>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatColor(uint color) =>
        string.Create(CultureInfo.InvariantCulture, $"#{(color >> RedShift) & 0xFF:X2}{(color >> GreenShift) & 0xFF:X2}{color & 0xFF:X2}");

    /// <summary>Reads comma separated flag names.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The flags; unknown names are ignored.</returns>
    internal static PdfAnnotationFlags ParseFlags(string? text)
    {
        var flags = PdfAnnotationFlags.None;
        if (string.IsNullOrWhiteSpace(text))
        {
            return flags;
        }

        var span = text.AsSpan();
        foreach (var range in span.Split(','))
        {
            flags |= FlagOf(span[range].Trim());
        }

        return flags;
    }

    /// <summary>Formats flags as comma separated names.</summary>
    /// <param name="flags">The flags.</param>
    /// <returns>The text; empty for no flags.</returns>
    internal static string FormatFlags(PdfAnnotationFlags flags)
    {
        var text = string.Empty;
        foreach (var (name, flag) in FlagNames)
        {
            if ((flags & flag) != 0)
            {
                text = text.Length == 0 ? name : $"{text},{name}";
            }
        }

        return text;
    }

    /// <summary>Reads a border style: a name such as <c>dash</c> or a PDF letter.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The PDF letter, or <see langword="null"/> when unknown.</returns>
    internal static string? ParseStyle(string? text)
    {
        foreach (var (name, letter) in StyleNames)
        {
            if (string.Equals(text, name, StringComparison.OrdinalIgnoreCase) || string.Equals(text, letter, StringComparison.OrdinalIgnoreCase))
            {
                return letter;
            }
        }

        return null;
    }

    /// <summary>Formats a PDF border style letter as an XFDF name.</summary>
    /// <param name="letter">The PDF letter.</param>
    /// <returns>The name, or <see langword="null"/> when unknown.</returns>
    internal static string? FormatStyle(string? letter)
    {
        foreach (var (name, candidate) in StyleNames)
        {
            if (string.Equals(letter, candidate, StringComparison.Ordinal))
            {
                return name;
            }
        }

        return null;
    }

    /// <summary>Reads a PDF date written as text.</summary>
    /// <param name="text">The text, such as <c>D:20260101120000+00'00'</c>.</param>
    /// <returns>The date, or <see langword="null"/> when unreadable.</returns>
    internal static DateTimeOffset? ParseDate(string? text)
    {
        if (string.IsNullOrEmpty(text) || text.Length > DateBytes)
        {
            return null;
        }

        Span<byte> bytes = stackalloc byte[DateBytes];
        var length = Encoding.UTF8.GetBytes(text, bytes);
        return PdfDate.Parse(bytes[..length]);
    }

    /// <summary>Formats a date as a PDF date.</summary>
    /// <param name="date">The date.</param>
    /// <returns>The text.</returns>
    internal static string FormatDate(DateTimeOffset date)
    {
        Span<byte> bytes = stackalloc byte[DateBytes];
        var length = PdfDate.Format(date, bytes);
        return Encoding.UTF8.GetString(bytes[..length]);
    }

    /// <summary>Reads <c>yes</c> and <c>no</c>, also <c>true</c> and <c>false</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The value, or <see langword="null"/> when neither.</returns>
    internal static bool? ParseBoolean(string? text) => text?.Trim().ToLowerInvariant() switch
    {
        "yes" or "true" or "1" => true,
        "no" or "false" or "0" => false,
        _ => null,
    };

    /// <summary>Formats a boolean as <c>yes</c> or <c>no</c>.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The text.</returns>
    internal static string FormatBoolean(bool value) => value ? "yes" : "no";

    /// <summary>Removes the characters XML 1.0 cannot carry: most control characters and unpaired surrogates.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text itself when it is clean, otherwise a copy without the bad characters.</returns>
    internal static string MakeXmlSafe(string text)
    {
        var bad = FirstInvalid(text);
        if (bad < 0)
        {
            return text;
        }

        var builder = new StringBuilder(text.Length);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            if (XmlConvert.IsXmlChar(c))
            {
                _ = builder.Append(c);
            }
            else if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], c))
            {
                _ = builder.Append(c).Append(text[i + 1]);
                i++;
            }
        }

        return builder.ToString();
    }

    /// <summary>Finds the first character XML cannot carry.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The index, or -1.</returns>
    private static int FirstInvalid(string text)
    {
        for (var i = 0; i < text.Length; i++)
        {
            if (XmlConvert.IsXmlChar(text[i]))
            {
                continue;
            }

            if (i + 1 < text.Length && XmlConvert.IsXmlSurrogatePair(text[i + 1], text[i]))
            {
                i++;
                continue;
            }

            return i;
        }

        return -1;
    }

    /// <summary>Finds the flag a name stands for.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The flag; none when unknown.</returns>
    private static PdfAnnotationFlags FlagOf(ReadOnlySpan<char> name)
    {
        foreach (var (candidate, flag) in FlagNames)
        {
            if (name.Equals(candidate, StringComparison.OrdinalIgnoreCase))
            {
                return flag;
            }
        }

        return PdfAnnotationFlags.None;
    }

    /// <summary>An XFDF flag name and its flag.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Flag">The flag.</param>
    private readonly record struct FlagName(string Name, PdfAnnotationFlags Flag);

    /// <summary>An XFDF border style name and its PDF letter.</summary>
    /// <param name="Name">The name.</param>
    /// <param name="Letter">The PDF letter.</param>
    private readonly record struct StyleName(string Name, string Letter);
}
