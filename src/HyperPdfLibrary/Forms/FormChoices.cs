// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>Reads the options and selection of a combo box or list box.</summary>
internal static class FormChoices
{
    /// <summary>The position of an option's export value in an option pair.</summary>
    private const int ValueSlot = 0;

    /// <summary>The position of an option's label in an option pair.</summary>
    private const int LabelSlot = 1;

    /// <summary>Counts a field's options.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The number of <c>/Opt</c> entries.</returns>
    internal static int Count(PdfDictionary field) => FieldAttributes.Find(field, KnownName.Opt).AsArray()?.Count ?? 0;

    /// <summary>Gets an option's export value.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The value; empty when the option has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetValue(PdfDictionary field, int index) => GetText(field, index, ValueSlot);

    /// <summary>Gets an option's label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns>The label; for an option that is a plain string, the string; empty when missing.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string GetLabel(PdfDictionary field, int index) => GetText(field, index, LabelSlot);

    /// <summary>Gets every option's label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The labels.</returns>
    internal static string[] GetLabels(PdfDictionary field)
    {
        var count = Count(field);
        if (count == 0)
        {
            return [];
        }

        var labels = new string[count];
        for (var i = 0; i < labels.Length; i++)
        {
            labels[i] = GetLabel(field, i);
        }

        return labels;
    }

    /// <summary>Finds the option with an export value.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="value">The export value.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FindValue(PdfDictionary field, string value)
    {
        var count = Count(field);
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(GetValue(field, i), value, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the option with a label.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="label">The label.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FindLabel(PdfDictionary field, string label)
    {
        var count = Count(field);
        for (var i = 0; i < count; i++)
        {
            if (string.Equals(GetLabel(field, i), label, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the first selected option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns>The option index, or -1.</returns>
    internal static int FirstSelected(PdfDictionary field)
    {
        var count = Count(field);
        var useIndices = UsesSelectedIndices(field);
        for (var i = 0; i < count; i++)
        {
            if (IsSelected(field, i, useIndices))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Determines whether an option is selected.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <param name="useIndices">Whether <c>/I</c> decides, rather than <c>/V</c>.</param>
    /// <returns><see langword="true"/> when selected.</returns>
    internal static bool IsSelected(PdfDictionary field, int index, bool useIndices) =>
        useIndices ? IsSelectedIndex(field, index) : IsSelectedValue(field, GetValue(field, index));

    /// <summary>
    /// Determines whether the field's <c>/I</c> entry describes the selection: it must exist and, when there is a
    /// <c>/V</c> entry, name exactly the options that <c>/V</c> names.
    /// </summary>
    /// <param name="field">The field dictionary.</param>
    /// <returns><see langword="true"/> when <c>/I</c> is trusted.</returns>
    internal static bool UsesSelectedIndices(PdfDictionary field)
    {
        var indices = FieldAttributes.Find(field, KnownName.I);
        if (indices.IsNull)
        {
            return false;
        }

        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.IsNull)
        {
            return true;
        }

        var counts = new Dictionary<string, int>(StringComparer.Ordinal);
        return TryCountValues(value, indices, counts) && IndicesMatch(field, indices, counts);
    }

    /// <summary>Reads an option's text at a position.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <param name="slot">The position within an option pair.</param>
    /// <returns>The text; empty when missing.</returns>
    private static string GetText(PdfDictionary field, int index, int slot)
    {
        if (FieldAttributes.Find(field, KnownName.Opt).AsArray() is not { } options)
        {
            return string.Empty;
        }

        var option = options.Get(index);
        if (option.AsArray() is { } pair)
        {
            option = pair.Get(slot);
        }

        return option.Kind == PdfKind.String ? PdfText.Decode(option.AsStringBytes()) : string.Empty;
    }

    /// <summary>Determines whether the value names an option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="optionValue">The option's export value.</param>
    /// <returns><see langword="true"/> when <c>/V</c> is that string, or an array holding it.</returns>
    private static bool IsSelectedValue(PdfDictionary field, string optionValue)
    {
        var value = FieldAttributes.Find(field, KnownName.V);
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array.Get(i).Kind == PdfKind.String && string.Equals(FieldAttributes.ReadText(array.Get(i)), optionValue, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }

        return value.Kind == PdfKind.String && string.Equals(FieldAttributes.ReadText(value), optionValue, StringComparison.Ordinal);
    }

    /// <summary>Determines whether <c>/I</c> lists an option.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="index">The option index.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private static bool IsSelectedIndex(PdfDictionary field, int index)
    {
        var indices = FieldAttributes.Find(field, KnownName.I);
        if (indices.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (array.Get(i).IsNumber && array.GetInt32(i) == index)
                {
                    return true;
                }
            }
        }

        return indices.IsNumber && indices.AsInt32() == index;
    }

    /// <summary>Counts the values <c>/V</c> holds, checking that there are as many as indices.</summary>
    /// <param name="value">The <c>/V</c> value.</param>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <param name="counts">Receives how many times each value occurs.</param>
    /// <returns><see langword="false"/> when the counts cannot match.</returns>
    private static bool TryCountValues(PdfValue value, PdfValue indices, Dictionary<string, int> counts)
    {
        var indexCount = CountIndices(indices);
        if (indexCount < 0)
        {
            return false;
        }

        if (value.AsArray() is { } array)
        {
            if (array.Count != indexCount)
            {
                return false;
            }

            for (var i = 0; i < array.Count; i++)
            {
                Increment(counts, array.Get(i));
            }

            return true;
        }

        if (value.Kind == PdfKind.String)
        {
            Increment(counts, value);
            return indexCount == 1;
        }

        return true;
    }

    /// <summary>Counts the indices <c>/I</c> holds.</summary>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <returns>The count; -1 when it is neither an array nor a number.</returns>
    private static int CountIndices(PdfValue indices)
    {
        if (indices.AsArray() is { } array)
        {
            return array.Count;
        }

        return indices.IsNumber ? 1 : -1;
    }

    /// <summary>Counts one string value.</summary>
    /// <param name="counts">The counts.</param>
    /// <param name="value">The value; ignored unless it is a string.</param>
    private static void Increment(Dictionary<string, int> counts, PdfValue value)
    {
        if (value.Kind != PdfKind.String)
        {
            return;
        }

        var text = FieldAttributes.ReadText(value);
        counts[text] = counts.GetValueOrDefault(text) + 1;
    }

    /// <summary>Checks that each listed index names an option whose value <c>/V</c> also names.</summary>
    /// <param name="field">The field dictionary.</param>
    /// <param name="indices">The <c>/I</c> value.</param>
    /// <param name="counts">The value counts, which are used up.</param>
    /// <returns><see langword="true"/> when both agree exactly.</returns>
    private static bool IndicesMatch(PdfDictionary field, PdfValue indices, Dictionary<string, int> counts)
    {
        var optionCount = Count(field);
        var array = indices.AsArray();
        var total = array?.Count ?? 1;
        for (var i = 0; i < total; i++)
        {
            var entry = array is null ? indices : array.Get(i);
            if (!entry.IsNumber || (uint)entry.AsInt32() >= (uint)optionCount || !Consume(counts, GetValue(field, entry.AsInt32())))
            {
                return false;
            }
        }

        return array is null ? counts.ContainsKey(GetValue(field, indices.AsInt32())) || counts.Count == 0 : counts.Count == 0;
    }

    /// <summary>Uses up one occurrence of a value.</summary>
    /// <param name="counts">The counts.</param>
    /// <param name="text">The value.</param>
    /// <returns><see langword="false"/> when the value was not counted.</returns>
    private static bool Consume(Dictionary<string, int> counts, string text)
    {
        if (!counts.TryGetValue(text, out var remaining))
        {
            return false;
        }

        if (remaining <= 1)
        {
            _ = counts.Remove(text);
        }
        else
        {
            counts[text] = remaining - 1;
        }

        return true;
    }
}
