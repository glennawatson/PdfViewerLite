// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml;

namespace HyperPdfLibrary.Interchange;

/// <summary>
/// Reads an XFDF document forward-only into a <see cref="PdfInterchangeData"/>. Elements it does not know are skipped.
/// Every handler leaves the reader after the element it handled.
/// </summary>
internal sealed partial class XfdfParser
{
    /// <summary>The reader.</summary>
    private readonly XmlReader _reader;

    /// <summary>The data being filled.</summary>
    private readonly PdfInterchangeData _data;

    /// <summary>The field being read, or <see langword="null"/> outside a field.</summary>
    private FieldFrame? _frame;

    /// <summary>Initializes a new instance of the <see cref="XfdfParser"/> class.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="data">The data to fill.</param>
    internal XfdfParser(XmlReader reader, PdfInterchangeData data)
    {
        _reader = reader;
        _data = data;
    }

    /// <summary>Handles one child element of the element being read.</summary>
    /// <param name="parser">The parser.</param>
    /// <param name="name">The child's local name.</param>
    /// <returns><see langword="true"/> when the handler consumed the child; otherwise the child is skipped.</returns>
    private delegate bool ChildHandler(XfdfParser parser, string name);

    /// <summary>Reads the document.</summary>
    /// <exception cref="PdfException">The document is not XFDF.</exception>
    internal void Parse()
    {
        if (_reader.MoveToContent() != XmlNodeType.Element || !string.Equals(_reader.LocalName, XfdfNames.Root, StringComparison.OrdinalIgnoreCase))
        {
            throw new PdfException(PdfError.Format, "The XML document is not XFDF: its root element is not xfdf.");
        }

        ReadChildren(static (parser, name) => parser.ReadRootChild(name));
    }

    /// <summary>Reads one child of the root.</summary>
    /// <param name="name">The child's name.</param>
    /// <returns><see langword="true"/> when consumed.</returns>
    private bool ReadRootChild(string name)
    {
        switch (name)
        {
            case XfdfNames.File:
            {
                _data.FileHref = _reader.GetAttribute("href");
                return false;
            }

            case XfdfNames.Ids:
            {
                _data.OriginalId = _reader.GetAttribute("original");
                _data.ModifiedId = _reader.GetAttribute("modified");
                return false;
            }

            case XfdfNames.Fields:
            {
                ReadChildren(static (parser, child) => parser.ReadFieldsChild(child, string.Empty, 0));
                return true;
            }

            case XfdfNames.Annots:
            {
                ReadChildren(static (parser, child) => parser.ReadAnnotation(child));
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Reads the children of the current element, which must be positioned on its start tag.</summary>
    /// <param name="handler">Handles each child element.</param>
    private void ReadChildren(ChildHandler handler)
    {
        if (_reader.IsEmptyElement)
        {
            _ = _reader.Read();
            return;
        }

        _ = _reader.Read();
        while (!_reader.EOF && _reader.NodeType != XmlNodeType.EndElement)
        {
            if (_reader.NodeType != XmlNodeType.Element)
            {
                _ = _reader.Read();
                continue;
            }

            if (!handler(this, _reader.LocalName))
            {
                _reader.Skip();
            }
        }

        _ = _reader.Read();
    }

    /// <summary>The field being read and the values gathered so far.</summary>
    /// <param name="name">The fully qualified name.</param>
    /// <param name="depth">How deeply the field nests.</param>
    private sealed class FieldFrame(string name, int depth)
    {
        /// <summary>The rich text value.</summary>
        private string? _richText;

        /// <summary>Gets the fully qualified name.</summary>
        internal string Name { get; } = name;

        /// <summary>Gets how deeply the field nests.</summary>
        internal int Depth { get; } = depth;

        /// <summary>Gets the values read.</summary>
        internal List<string> Values { get; } = [];

        /// <summary>Gets the rich text value, or <see langword="null"/>.</summary>
        internal string? RichText => _richText;

        /// <summary>Sets the rich text value.</summary>
        /// <param name="text">The XHTML, or <see langword="null"/>.</param>
        internal void SetRichText(string? text) => _richText = text;
    }
}
