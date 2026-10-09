// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <content>Marked content and optional content.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>Handles <c>BMC</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpBeginMarkedContent(ContentInterpreter self, ref ContentReader reader) => self.BeginMarked(reader.Operand(0).Name, null, default);

    /// <summary>Handles <c>BDC</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpBeginMarkedContentProperties(ContentInterpreter self, ref ContentReader reader)
    {
        var tag = reader.Operand(0).Name;
        var properties = reader.Operand(1);
        if (properties.Kind == ContentOperandKind.Dictionary)
        {
            // An inline property list, such as << /MCID 3 >> or << /ActualText (fi) >>, is parsed so the device sees it.
            // Parsing it as a window copies out only its strings, not the whole list.
            var parser = PdfParser.ForWindow(reader.Body(properties), 0, self._cache.Document.Objects, self._cache.Names, null);
            self.BeginMarked(tag, parser.ParseValue().AsDictionary(), default);
            return;
        }

        if (properties.Kind != ContentOperandKind.Name)
        {
            self.BeginMarked(tag, null, default);
            return;
        }

        var name = properties.Name;
        self.BeginMarked(tag, self.FindResource(KnownName.Properties, name).AsDictionary(), self.FindResourceRaw(KnownName.Properties, name));
    }

    /// <summary>Handles <c>EMC</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpEndMarkedContent(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._marked.Count > self._markedFloor)
        {
            self.EndMarked();
        }
    }

    /// <summary>Opens a marked content level, hiding it when it belongs to a hidden layer.</summary>
    /// <param name="tag">The tag.</param>
    /// <param name="properties">The property list, or null.</param>
    /// <param name="membership">The unresolved property list entry, which an optional content tag refers to.</param>
    private void BeginMarked(PdfName tag, PdfDictionary? properties, PdfValue membership)
    {
        var hide = tag.Is(KnownName.OC) && !membership.IsNull && !IsLayerShown(membership);
        _marked.Add(hide);
        if (hide)
        {
            _hidden++;
        }

        _device.BeginMarkedContent(_cache.Names.GetSpelling(tag), properties);
    }

    /// <summary>Closes the innermost marked content level.</summary>
    private void EndMarked()
    {
        var last = _marked.Count - 1;
        if (_marked[last])
        {
            _hidden--;
        }

        _marked.RemoveAt(last);
        _device.EndMarkedContent();
    }

    /// <summary>Finds a named resource without following references.</summary>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The value as stored; null when missing.</returns>
    private PdfValue FindResourceRaw(KnownName category, PdfName name)
    {
        for (var i = _resources.Count - 1; i >= 0; i--)
        {
            var value = _resources[i]?.GetDictionary(category)?.GetRaw(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
