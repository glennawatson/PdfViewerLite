// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Compares values by content, descending into direct arrays, dictionaries and streams but not following references,
/// so a freshly parsed object and an edited copy of it compare equal when nothing changed.
/// </summary>
internal static class PdfValueEquality
{
    /// <summary>Determines whether two values hold the same content.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <returns><see langword="true"/> when they do.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equal(PdfValue left, PdfValue right) => Equal(left, right, 0);

    /// <summary>Determines whether a key holds the same content in two dictionaries.</summary>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool KeyEqual(PdfDictionary? left, PdfDictionary? right, PdfName key) =>
        Equal(left?.GetRaw(key) ?? default, right?.GetRaw(key) ?? default, 0);

    /// <summary>Determines whether two dictionaries differ in any key other than some ignored ones.</summary>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <param name="ignored">The keys left out of the comparison.</param>
    /// <returns><see langword="true"/> when another key differs.</returns>
    internal static bool DiffersOutside(PdfDictionary left, PdfDictionary right, ReadOnlySpan<KnownName> ignored) =>
        HasDifferentKey(left, right, ignored) || HasDifferentKey(right, left, ignored);

    /// <summary>Determines whether a key of one dictionary is missing from or different in another.</summary>
    /// <param name="source">The dictionary whose keys are checked.</param>
    /// <param name="other">The other dictionary.</param>
    /// <param name="ignored">The keys left out.</param>
    /// <returns><see langword="true"/> when one differs.</returns>
    private static bool HasDifferentKey(PdfDictionary source, PdfDictionary other, ReadOnlySpan<KnownName> ignored)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (!ignored.Contains(key.ToKnownName()) && !Equal(source.GetValueAt(i), other.GetRaw(key), 0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Compares two values.</summary>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they hold the same content.</returns>
    private static bool Equal(PdfValue left, PdfValue right, int depth)
    {
        if (left.Kind != right.Kind)
        {
            return false;
        }

        if (left.Equals(right))
        {
            return true;
        }

        return depth <= PdfLimits.MaxNesting && left.Kind switch
        {
            PdfKind.Reference => left.AsReference().Number == right.AsReference().Number,
            PdfKind.String => left.AsStringBytes().SequenceEqual(right.AsStringBytes()),
            PdfKind.Array => ArraysEqual(left.AsArray()!, right.AsArray()!, depth),
            PdfKind.Dictionary => DictionariesEqual(left.AsDictionary()!, right.AsDictionary()!, depth),
            PdfKind.Stream => StreamsEqual(left.AsStream()!, right.AsStream()!, depth),
            _ => false,
        };
    }

    /// <summary>Compares two arrays item by item.</summary>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they hold the same content.</returns>
    private static bool ArraysEqual(PdfArray left, PdfArray right, int depth)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!Equal(left.GetRaw(i), right.GetRaw(i), depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two dictionaries key by key, ignoring order.</summary>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they hold the same content.</returns>
    private static bool DictionariesEqual(PdfDictionary left, PdfDictionary right, int depth)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!Equal(left.GetValueAt(i), right.GetRaw(left.GetKeyAt(i)), depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two streams by dictionary and raw data.</summary>
    /// <param name="left">The first stream.</param>
    /// <param name="right">The second stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when they hold the same content.</returns>
    private static bool StreamsEqual(PdfStream left, PdfStream right, int depth) =>
        ReferenceEquals(left, right)
        || (left.IsEncrypted == right.IsEncrypted && RawDataEqual(left, right) && DictionariesEqual(left.Dictionary, right.Dictionary, depth + 1));

    /// <summary>Compares the encoded data of two streams.</summary>
    /// <param name="left">The first stream.</param>
    /// <param name="right">The second stream.</param>
    /// <returns><see langword="true"/> when the bytes match.</returns>
    private static bool RawDataEqual(PdfStream left, PdfStream right)
    {
        if (left.RawLength != right.RawLength)
        {
            return false;
        }

        using var leftData = left.LeaseRawData();
        using var rightData = right.LeaseRawData();
        return leftData.Span.SequenceEqual(rightData.Span);
    }
}
