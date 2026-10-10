// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Marked operations over its owned state.</summary>
internal static class ContentMarked
{
    /// <summary>Handles <c>BMC</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpBeginMarkedContent(ContentInterpreter self, ref ContentReader reader) => ContentMarked.BeginMarked(self, reader.Operand(0).Name, null, default);

    /// <summary>Handles <c>BDC</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpBeginMarkedContentProperties(ContentInterpreter self, ref ContentReader reader)
    {
        var tag = reader.Operand(0).Name;
        var properties = reader.Operand(1);
        if (properties.Kind == ContentOperandKind.Dictionary)
        {
            // An inline property list, such as << /MCID 3 >> or << /ActualText (fi) >>, is parsed so the device sees it.
            // Parsing it as a window copies out only its strings, not the whole list.
            var parser = PdfParser.ForWindow(reader.Body(properties), 0, self.Cache.Document.Objects, self.Cache.Names, null);
            ContentMarked.BeginMarked(self, tag, parser.ParseValue().AsDictionary(), default);
            return;
        }

        if (properties.Kind != ContentOperandKind.Name)
        {
            ContentMarked.BeginMarked(self, tag, null, default);
            return;
        }

        var name = properties.Name;
        ContentMarked.BeginMarked(self, tag, ContentExecution.FindResource(self, KnownName.Properties, name).AsDictionary(), ContentMarked.FindResourceRaw(self, KnownName.Properties, name));
    }

    /// <summary>Handles <c>EMC</c>.</summary>
    /// <param name = "self">The interpreter.</param>
    /// <param name = "reader">The reader.</param>
    internal static void OpEndMarkedContent(ContentInterpreter self, ref ContentReader reader)
    {
        if (self.Marked.Count > self.MarkedFloor)
        {
            ContentMarked.EndMarked(self);
        }
    }

    /// <summary>Opens a marked content level, hiding it when it belongs to a hidden layer.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "tag">The tag.</param>
    /// <param name = "properties">The property list, or null.</param>
    /// <param name = "membership">The unresolved property list entry, which an optional content tag refers to.</param>
    internal static void BeginMarked(ContentInterpreter self, PdfName tag, PdfDictionary? properties, PdfValue membership)
    {
        var hide = tag.Is(KnownName.OC) && !membership.IsNull && !ContentXObjects.IsLayerShown(self, membership);
        self.Marked.Add(hide);
        if (hide)
        {
            self.Hidden++;
        }

        self.Device.BeginMarkedContent(self.Cache.Names.GetSpelling(tag), properties);
    }

    /// <summary>Closes the innermost marked content level.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    internal static void EndMarked(ContentInterpreter self)
    {
        var last = self.Marked.Count - 1;
        if (self.Marked[last])
        {
            self.Hidden--;
        }

        self.Marked.RemoveAt(last);
        self.Device.EndMarkedContent();
    }

    /// <summary>Finds a named resource without following references.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "category">The resource category.</param>
    /// <param name = "name">The resource name.</param>
    /// <returns>The value as stored; null when missing.</returns>
    internal static PdfValue FindResourceRaw(ContentInterpreter self, KnownName category, PdfName name)
    {
        for (var i = self.Resources.Count - 1; i >= 0; i--)
        {
            var value = self.Resources[i]?.GetDictionary(category)?.GetRaw(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
