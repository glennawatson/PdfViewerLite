// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Interchange;

/// <content>The <c>fields</c> element.</content>
internal sealed partial class XfdfParser
{
    /// <summary>The attribute naming a field.</summary>
    private const string NameAttribute = "name";

    /// <summary>Turns an empty string into <see langword="null"/>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The text, or <see langword="null"/> when empty.</returns>
    private static string? NullIfEmpty(string text) => text.Length == 0 ? null : text;

    /// <summary>Reads a child of <c>fields</c> or of a <c>field</c>.</summary>
    /// <param name="name">The child's name.</param>
    /// <param name="prefix">The fully qualified name of the parent field; empty at the top.</param>
    /// <param name="depth">How deeply the parent nests.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    /// <exception cref="PdfException">The fields nest deeper than the limit.</exception>
    private bool ReadFieldsChild(string name, string prefix, int depth)
    {
        if (!string.Equals(name, XfdfNames.Field, StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        if (depth >= PdfLimits.MaxNesting)
        {
            throw new PdfException(PdfError.Format, "The XFDF fields nest too deeply.");
        }

        var full = InterchangeFieldTree.Join(prefix, _reader.GetAttribute(NameAttribute) ?? string.Empty);
        var saved = _frame;
        var frame = new FieldFrame(full, depth);
        _frame = frame;
        ReadChildren(static (parser, child) => parser.ReadFieldChild(child));
        _frame = saved;
        if (full.Length > 0 && (frame.Values.Count > 0 || frame.RichText is not null))
        {
            _data.Fields.Add(new(full, [.. frame.Values]) { RichText = frame.RichText });
        }

        return true;
    }

    /// <summary>Reads a child of the field being read: a value or a nested field.</summary>
    /// <param name="name">The child's name.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool ReadFieldChild(string name)
    {
        var frame = _frame!;
        if (string.Equals(name, XfdfNames.Value, StringComparison.OrdinalIgnoreCase))
        {
            frame.Values.Add(_reader.ReadElementContentAsString());
            return true;
        }

        if (string.Equals(name, XfdfNames.ValueRichText, StringComparison.OrdinalIgnoreCase))
        {
            frame.SetRichText(NullIfEmpty(_reader.ReadInnerXml()));
            return true;
        }

        return ReadFieldsChild(name, frame.Name, frame.Depth + 1);
    }
}
