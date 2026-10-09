// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.PageObjects;

/// <content>Marked content.</content>
internal sealed partial class PageContentParser
{
    /// <summary>Handles <c>BMC</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpBeginMarkedContent(PageContentParser self, ref ContentReader reader) => self.Open(reader.Operand(0).Name, null, default);

    /// <summary>Handles <c>BDC</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpBeginMarkedContentProperties(PageContentParser self, ref ContentReader reader)
    {
        var tag = reader.Operand(0).Name;
        var properties = reader.Operand(1);
        if (properties.Kind == ContentOperandKind.Dictionary)
        {
            // An inline property list, such as << /MCID 3 >> or << /ActualText (fi) >>, is parsed so callers can read it.
            var parser = new PdfParser(reader.Body(properties).ToArray(), 0, self._owner.Document.Objects, self._names);
            self.Open(tag, parser.ParseValue().AsDictionary(), default);
            return;
        }

        if (properties.Kind != ContentOperandKind.Name)
        {
            self.Open(tag, null, default);
            return;
        }

        self.Open(tag, self.FindResource(KnownName.Properties, properties.Name).AsDictionary(), properties.Name);
    }

    /// <summary>Handles <c>EMC</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpEndMarkedContent(PageContentParser self, ref ContentReader reader)
    {
        if (self._marks.Count == 0)
        {
            return;
        }

        self._marks.RemoveAt(self._marks.Count - 1);
        self._markSnapshot = null;
    }

    /// <summary>Opens a marked-content sequence.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="properties">The property list, or null.</param>
    /// <param name="propertiesName">The property list's resource name, or none.</param>
    private void Open(PdfName tag, PdfDictionary? properties, PdfName propertiesName)
    {
        var mark = new PdfMark(tag, properties, propertiesName) { BeginSource = new(_operatorStart, _operatorEnd) };
        _marks.Add(mark);
        _owner.Begins.Add(mark);
        _markSnapshot = null;
    }
}
