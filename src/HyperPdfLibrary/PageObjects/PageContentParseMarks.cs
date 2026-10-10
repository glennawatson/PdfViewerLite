// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Tracks marked-content sequences while parsing operators.</summary>
internal static class PageContentParseMarks
{
    /// <summary>Handles <c>BMC</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpBeginMarkedContent(PageContentParseState self, ref ContentReader reader) => PageContentParseMarks.Open(self, reader.Operand(0).Name, null, default);

    /// <summary>Handles <c>BDC</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpBeginMarkedContentProperties(PageContentParseState self, ref ContentReader reader)
    {
        var tag = reader.Operand(0).Name;
        var properties = reader.Operand(1);
        if (properties.Kind == ContentOperandKind.Dictionary)
        {
            // An inline property list, such as << /MCID 3 >> or << /ActualText (fi) >>, is parsed so callers can read it.
            var parser = new PdfParser(reader.Body(properties).ToArray(), 0, self.Owner.Document.Objects, self.Names);
            PageContentParseMarks.Open(self, tag, parser.ParseValue().AsDictionary(), default);
            return;
        }

        if (properties.Kind != ContentOperandKind.Name)
        {
            PageContentParseMarks.Open(self, tag, null, default);
            return;
        }

        PageContentParseMarks.Open(self, tag, PageContentParseObjects.FindResource(self, KnownName.Properties, properties.Name).AsDictionary(), properties.Name);
    }

    /// <summary>Handles <c>EMC</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpEndMarkedContent(PageContentParseState self, ref ContentReader reader)
    {
        if (self.Marks.Count == 0)
        {
            return;
        }

        self.Marks.RemoveAt(self.Marks.Count - 1);
        self.MarkSnapshot = null;
    }

    /// <summary>Opens a marked-content sequence.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "tag">The tag.</param>
    /// <param name = "properties">The property list, or null.</param>
    /// <param name = "propertiesName">The property list's resource name, or none.</param>
    internal static void Open(PageContentParseState state, PdfName tag, PdfDictionary? properties, PdfName propertiesName)
    {
        var mark = new PdfMark(tag, properties, propertiesName) { BeginSource = new(state.OperatorStart, state.OperatorEnd), };
        state.Marks.Add(mark);
        state.Owner.Begins.Add(mark);
        state.MarkSnapshot = null;
    }
}
