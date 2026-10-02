// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using PdfViewerLite.Core.Reading;

namespace PdfViewerLite.Pdfium;

/// <summary>Maps the standard structure types of PDF 1.7 and PDF 2.0 (after the document's role map) to reading roles.</summary>
internal static class StructureRoles
{
    /// <summary>The deepest heading level.</summary>
    private const int MaxHeadingLevel = 6;

    /// <summary>Types that only group other elements.</summary>
    private static readonly FrozenSet<string> Groupings = new[]
    {
        "Document", "DocumentFragment", "Part", "Art", "Sect", "Div", "Aside", "L", "Table", "TR", "THead", "TBody", "TFoot", "TOC", "Index", "NonStruct", "Private", "Form",
    }.ToFrozenSet(StringComparer.Ordinal);

    /// <summary>Block types and what they read as.</summary>
    private static readonly FrozenDictionary<string, ReadingBlockKind> Blocks = new Dictionary<string, ReadingBlockKind>(StringComparer.Ordinal)
    {
        ["P"] = ReadingBlockKind.Paragraph,
        ["BlockQuote"] = ReadingBlockKind.Paragraph,
        ["Quote"] = ReadingBlockKind.Paragraph,
        ["Code"] = ReadingBlockKind.Paragraph,
        ["TOCI"] = ReadingBlockKind.Paragraph,
        ["Span"] = ReadingBlockKind.Paragraph,
        ["Link"] = ReadingBlockKind.Paragraph,
        ["Reference"] = ReadingBlockKind.Paragraph,
        ["Annot"] = ReadingBlockKind.Paragraph,
        ["Sub"] = ReadingBlockKind.Paragraph,
        ["Em"] = ReadingBlockKind.Paragraph,
        ["Strong"] = ReadingBlockKind.Paragraph,
        ["LI"] = ReadingBlockKind.ListItem,
        ["Lbl"] = ReadingBlockKind.ListItem,
        ["LBody"] = ReadingBlockKind.ListItem,
        ["TD"] = ReadingBlockKind.TableCell,
        ["TH"] = ReadingBlockKind.TableCell,
        ["Caption"] = ReadingBlockKind.Caption,
        ["Note"] = ReadingBlockKind.Footnote,
        ["FENote"] = ReadingBlockKind.Footnote,
        ["Figure"] = ReadingBlockKind.Figure,
        ["Formula"] = ReadingBlockKind.Figure,
    }.ToFrozenDictionary(StringComparer.Ordinal);

    /// <summary>Looks block types up by span.</summary>
    private static readonly FrozenDictionary<string, ReadingBlockKind>.AlternateLookup<ReadOnlySpan<char>> BlockLookup = Blocks.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Looks grouping types up by span, so classifying an element allocates nothing.</summary>
    private static readonly FrozenSet<string>.AlternateLookup<ReadOnlySpan<char>> GroupingLookup = Groupings.GetAlternateLookup<ReadOnlySpan<char>>();

    /// <summary>Works out what an element type means.</summary>
    /// <param name="type">The type, after role mapping.</param>
    /// <returns>The role.</returns>
    internal static StructureRole Classify(ReadOnlySpan<char> type)
    {
        if (GroupingLookup.Contains(type))
        {
            return new(ReadingBlockKind.Paragraph, 0, true, false);
        }

        if (type is "Title" or "H")
        {
            return new(ReadingBlockKind.Heading, 1, false, false);
        }

        if (type.Length == 2 && type[0] == 'H' && type[1] is >= '1' and <= '9')
        {
            return new(ReadingBlockKind.Heading, Math.Min(type[1] - '0', MaxHeadingLevel), false, false);
        }

        return BlockLookup.TryGetValue(type, out var kind)
            ? new(kind, 0, false, false)
            : new(ReadingBlockKind.Paragraph, 0, false, true);
    }
}
