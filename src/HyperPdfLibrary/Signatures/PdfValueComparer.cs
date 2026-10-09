// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Compares values from two revisions of a document. Names are compared by spelling, as each revision has its own name
/// table; references by object number, without following them, as each referenced object is compared on its own.
/// </summary>
internal static class PdfValueComparer
{
    /// <summary>How far apart two numbers may be and still count as the same, as writers round reals differently.</summary>
    private const double NumberTolerance = 1e-9;

    /// <summary>Compares two values.</summary>
    /// <param name="left">A value from the first revision.</param>
    /// <param name="leftNames">The first revision's name table.</param>
    /// <param name="right">A value from the second revision.</param>
    /// <param name="rightNames">The second revision's name table.</param>
    /// <returns><see langword="true"/> when the values are the same.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static bool Equal(PdfValue left, PdfNameTable leftNames, PdfValue right, PdfNameTable rightNames) =>
        Equal(new(leftNames, rightNames), left, right, 0);

    /// <summary>Compares two dictionaries, skipping one key.</summary>
    /// <param name="tables">The two name tables.</param>
    /// <param name="left">The first dictionary.</param>
    /// <param name="right">The second dictionary.</param>
    /// <param name="skip">The spelling of the key to skip, or empty to compare every key.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the dictionaries are the same.</returns>
    internal static bool DictionaryEqual(NameTables tables, PdfDictionary left, PdfDictionary right, ReadOnlySpan<byte> skip, int depth)
    {
        var skipped = 0;
        for (var i = 0; i < left.Count; i++)
        {
            var spelling = tables.Left.GetSpelling(left.GetKeyAt(i));
            if (!skip.IsEmpty && spelling.SequenceEqual(skip))
            {
                skipped++;
                continue;
            }

            var other = right.GetRaw(tables.Right.Intern(spelling));
            if (other.IsNull || !Equal(tables, left.GetValueAt(i), other, depth + 1))
            {
                return false;
            }
        }

        var rightSkipped = skip.IsEmpty || !right.ContainsKey(tables.Right.Intern(skip)) ? 0 : 1;
        return left.Count - skipped == right.Count - rightSkipped;
    }

    /// <summary>Compares two values.</summary>
    /// <param name="tables">The two name tables.</param>
    /// <param name="left">The first value.</param>
    /// <param name="right">The second value.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the values are the same.</returns>
    private static bool Equal(NameTables tables, PdfValue left, PdfValue right, int depth)
    {
        if (depth > PdfLimits.MaxNesting)
        {
            return false;
        }

        if (left.IsNumber && right.IsNumber)
        {
            return Math.Abs(left.AsNumber() - right.AsNumber()) <= NumberTolerance;
        }

        if (left.Kind != right.Kind)
        {
            return false;
        }

        return left.Kind switch
        {
            PdfKind.Name => tables.Left.GetSpelling(left.AsName()).SequenceEqual(tables.Right.GetSpelling(right.AsName())),
            PdfKind.String => left.AsStringBytes().SequenceEqual(right.AsStringBytes()),
            PdfKind.Array => ArrayEqual(tables, left.AsArray()!, right.AsArray()!, depth),
            PdfKind.Dictionary => DictionaryEqual(tables, left.AsDictionary()!, right.AsDictionary()!, [], depth),
            PdfKind.Stream => StreamEqual(tables, left.AsStream()!, right.AsStream()!, depth),
            _ => left.Equals(right),
        };
    }

    /// <summary>Compares two arrays item by item.</summary>
    /// <param name="tables">The two name tables.</param>
    /// <param name="left">The first array.</param>
    /// <param name="right">The second array.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the arrays are the same.</returns>
    private static bool ArrayEqual(NameTables tables, PdfArray left, PdfArray right, int depth)
    {
        if (left.Count != right.Count)
        {
            return false;
        }

        for (var i = 0; i < left.Count; i++)
        {
            if (!Equal(tables, left.GetRaw(i), right.GetRaw(i), depth + 1))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Compares two streams: their dictionaries, apart from /Length, and their data.</summary>
    /// <param name="tables">The two name tables.</param>
    /// <param name="left">The first stream.</param>
    /// <param name="right">The second stream.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the streams are the same.</returns>
    private static bool StreamEqual(NameTables tables, PdfStream left, PdfStream right, int depth)
    {
        if (!DictionaryEqual(tables, left.Dictionary, right.Dictionary, "Length"u8, depth))
        {
            return false;
        }

        if (RawEqual(left, right))
        {
            return true;
        }

        // Encrypted streams written again get a fresh IV, so equal content can have different raw bytes.
        return left.IsEncrypted && right.IsEncrypted && left.DecodeToArray().AsSpan().SequenceEqual(right.DecodeToArray());
    }

    /// <summary>Compares the encoded bytes of two streams, read through their files.</summary>
    /// <param name="left">The first stream.</param>
    /// <param name="right">The second stream.</param>
    /// <returns><see langword="true"/> when the bytes match.</returns>
    private static bool RawEqual(PdfStream left, PdfStream right)
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
