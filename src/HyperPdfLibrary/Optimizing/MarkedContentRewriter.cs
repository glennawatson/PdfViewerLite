// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// Finds a page's top-level drawing operators and wraps each in marked content: text-showing operators, image and form
/// XObjects, and inline images. Every other byte of the content is copied as it is, so the page draws exactly as before.
/// </summary>
internal static class MarkedContentRewriter
{
    /// <summary>Lists the operators to wrap, in content order.</summary>
    /// <param name="content">The page's decoded content.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="store">The store references are resolved in.</param>
    /// <returns>The units.</returns>
    internal static List<TagUnit> FindUnits(ReadOnlySpan<byte> content, PdfNameTable names, PdfDictionary? resources, PdfObjectStore store)
    {
        var units = new List<TagUnit>();
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, names, operands);
        var start = 0;
        while (reader.Next(out var op))
        {
            var end = reader.Position;
            if (Classify(op, ref reader, resources, store) is { } kind)
            {
                units.Add(new(start, end, kind));
            }

            start = end;
        }

        return units;
    }

    /// <summary>Writes the content with each unit wrapped in its marked content.</summary>
    /// <param name="content">The page's decoded content.</param>
    /// <param name="units">The units, in content order.</param>
    /// <param name="labels">How each unit is marked.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="output">Receives the new content.</param>
    internal static void Write(ReadOnlySpan<byte> content, List<TagUnit> units, ReadOnlySpan<TagLabel> labels, PdfNameTable names, ref PooledBuffer output)
    {
        var position = 0;
        for (var i = 0; i < units.Count; i++)
        {
            var (start, end, _) = units[i];
            output.Write(content[position..start]);
            WriteOpen(labels[i], names, ref output);
            output.Write(content[start..end]);
            output.Write("\nEMC\n"u8);
            position = end;
        }

        output.Write(content[position..]);
    }

    /// <summary>Writes the operator that opens a unit's marked content.</summary>
    /// <param name="label">How the unit is marked.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="output">The output.</param>
    private static void WriteOpen(TagLabel label, PdfNameTable names, ref PooledBuffer output)
    {
        if (label.IsArtifact)
        {
            output.Write("\n/Artifact BMC\n"u8);
            return;
        }

        output.WriteByte((byte)'\n');
        PdfSyntax.WriteName(ref output, names.GetSpelling(label.Tag));
        output.Write(" <</MCID "u8);
        PdfSyntax.WriteInteger(ref output, label.Mcid);
        output.Write(">> BDC\n"u8);
    }

    /// <summary>Decides whether an operator is wrapped, and as what.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="store">The store references are resolved in.</param>
    /// <returns>The unit's kind, or <see langword="null"/> when the operator is not wrapped.</returns>
    private static TagUnitKind? Classify(ContentOperator op, ref ContentReader reader, PdfDictionary? resources, PdfObjectStore store)
    {
        switch (op)
        {
            case ContentOperator.ShowText or ContentOperator.ShowTextArray or ContentOperator.NextLineShowText or ContentOperator.SetSpacingNextLineShowText:
                {
                    return TagUnitKind.Text;
                }

            case ContentOperator.BeginInlineImage:
                {
                    return TagUnitKind.Figure;
                }

            case ContentOperator.PaintXObject:
                {
                    return ClassifyXObject(reader.Operand(0).Name, resources, store);
                }

            default:
                {
                    return null;
                }
        }
    }

    /// <summary>Decides what a <c>Do</c> draws.</summary>
    /// <param name="name">The XObject's resource name.</param>
    /// <param name="resources">The page's resources.</param>
    /// <param name="store">The store references are resolved in.</param>
    /// <returns>A figure for an image, a form unit for a form, or <see langword="null"/>.</returns>
    private static TagUnitKind? ClassifyXObject(PdfName name, PdfDictionary? resources, PdfObjectStore store)
    {
        var raw = resources?.GetDictionary(KnownName.XObject)?.GetRaw(name) ?? default;
        var subtype = StoreReading.Resolve(store, raw).AsStream()?.Dictionary.GetName(KnownName.Subtype) ?? default;
        return subtype.ToKnownName() switch
        {
            KnownName.Image => TagUnitKind.Figure,
            KnownName.Form => TagUnitKind.Form,
            _ => null,
        };
    }
}
