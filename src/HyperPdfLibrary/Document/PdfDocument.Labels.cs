// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Page labels, numbered the way PDFium numbers them.</content>
public sealed partial class PdfDocument
{
    /// <summary>The letters in the alphabet styles.</summary>
    private const int LetterCount = 26;

    /// <summary>The largest number written in Roman numerals before wrapping, as PDFium does.</summary>
    private const int MaxRoman = 1_000_000;

    /// <summary>The longest run of letters written, as PDFium limits it.</summary>
    private const int MaxLetters = 1000;

    /// <summary>The label ranges, read on first use: start page and label dictionary, sorted by start.</summary>
    private LabelRange[]? _labelRanges;

    /// <summary>Gets the values of the Roman numeral symbols, largest first.</summary>
    private static ReadOnlySpan<int> RomanValues => [0x3E8, 0x384, 0x1F4, 0x190, 0x64, 0x5A, 0x32, 0x28, 0x0A, 0x09, 0x05, 0x04, 0x01];

    /// <summary>Gets the lower-case Roman numeral symbols, matching <see cref="RomanValues"/>.</summary>
    private static string[] RomanSymbols { get; } = ["m", "cm", "d", "cd", "c", "xc", "l", "xl", "x", "ix", "v", "iv", "i"];

    /// <summary>Gets a page's label, for example "iv".</summary>
    /// <param name="index">The zero based page index.</param>
    /// <returns>The label, or <see langword="null"/> when the document defines no labels or the label is empty.</returns>
    public string? GetPageLabel(int index)
    {
        if ((uint)index >= (uint)PageCount || Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        var ranges = GetLabelRanges();
        var range = FindRange(ranges, index);
        if (range is null)
        {
            return (index + 1).ToString(CultureInfo.InvariantCulture);
        }

        var dictionary = range.Value.Label;
        var prefix = dictionary.GetText(KnownName.P) ?? string.Empty;
        var number = index - range.Value.Start + dictionary.GetInt32(KnownName.St, 1);
        var label = prefix + FormatNumber(number, Objects.Names.GetSpelling(dictionary.GetName(KnownName.S)));
        return label.Length == 0 ? null : label;
    }

    /// <summary>Gets the labels of some pages as style and number, for pages copied into another document.</summary>
    /// <param name="pages">The pages.</param>
    /// <returns>The label of each page, or <see langword="null"/> when the document defines no labels.</returns>
    internal PdfPageLabel[]? CollectSourceLabels(ReadOnlySpan<PdfPage> pages)
    {
        if (Catalog.GetDictionary(KnownName.PageLabels) is null)
        {
            return null;
        }

        // Pages before the first range are labelled with their decimal page number; one shared style keeps them in runs.
        var ranges = GetLabelRanges();
        var decimalStyle = new PdfDictionary(Objects);
        decimalStyle.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        var labels = new PdfPageLabel[pages.Length];
        for (var i = 0; i < labels.Length; i++)
        {
            var index = pages[i].Index;
            labels[i] = FindRange(ranges, index) is { } range
                ? new(range.Label, index - range.Start + range.Label.GetInt32(KnownName.St, 1))
                : new(decimalStyle, index + 1);
        }

        return labels;
    }

    /// <summary>Finds the range with the largest start at or before a page.</summary>
    /// <param name="ranges">The sorted ranges.</param>
    /// <param name="index">The page index.</param>
    /// <returns>The range, or <see langword="null"/>.</returns>
    private static LabelRange? FindRange(LabelRange[] ranges, int index)
    {
        LabelRange? found = null;
        foreach (var range in ranges)
        {
            if (range.Start > index)
            {
                break;
            }

            found = range;
        }

        return found;
    }

    /// <summary>Formats the number part of a label.</summary>
    /// <param name="number">The number.</param>
    /// <param name="spelling">The /S style name's spelling.</param>
    /// <returns>The text.</returns>
    private static string FormatNumber(int number, ReadOnlySpan<byte> spelling) =>
        spelling.Length != 1 ? string.Empty : spelling[0] switch
        {
            (byte)'D' => number.ToString(CultureInfo.InvariantCulture),
            (byte)'R' => Roman(number).ToUpperInvariant(),
            (byte)'r' => Roman(number),
            (byte)'A' => Letters(number).ToUpperInvariant(),
            (byte)'a' => Letters(number),
            _ => string.Empty,
        };

    /// <summary>Writes a number in lower-case Roman numerals.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The numerals.</returns>
    private static string Roman(int number)
    {
        number %= MaxRoman;
        var builder = new StringBuilder();
        for (var i = 0; i < RomanValues.Length && number > 0; i++)
        {
            while (number >= RomanValues[i])
            {
                number -= RomanValues[i];
                _ = builder.Append(RomanSymbols[i]);
            }
        }

        return builder.ToString();
    }

    /// <summary>Writes a number as letters: a..z, then aa..zz, and so on.</summary>
    /// <param name="number">The number.</param>
    /// <returns>The letters.</returns>
    private static string Letters(int number)
    {
        if (number <= 0)
        {
            return string.Empty;
        }

        var zeroBased = number - 1;
        var count = ((zeroBased / LetterCount) + 1) % MaxLetters;
        return new((char)('a' + (zeroBased % LetterCount)), count);
    }

    /// <summary>Reads the label ranges once.</summary>
    /// <returns>The ranges, sorted by start page.</returns>
    private LabelRange[] GetLabelRanges()
    {
        var ranges = Volatile.Read(ref _labelRanges);
        if (ranges is not null)
        {
            return ranges;
        }

        var entries = new List<NameTreeEntry>();
        NameTree.EnumerateNumbers(Catalog.GetDictionary(KnownName.PageLabels), entries);
        var list = new List<LabelRange>(entries.Count);
        foreach (var entry in entries)
        {
            if (entry.Key.IsNumber && entry.Value.AsDictionary() is { } label)
            {
                list.Add(new(entry.Key.AsInt32(), label));
            }
        }

        list.Sort(static (a, b) => a.Start.CompareTo(b.Start));
        ranges = [.. list];
        Volatile.Write(ref _labelRanges, ranges);
        return ranges;
    }

    /// <summary>A run of pages sharing a label style.</summary>
    /// <param name="Start">The first page of the run.</param>
    /// <param name="Label">The label dictionary.</param>
    private readonly record struct LabelRange(int Start, PdfDictionary Label);
}
