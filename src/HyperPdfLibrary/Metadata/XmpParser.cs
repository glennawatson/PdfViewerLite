// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using System.Xml;
using HyperPdfLibrary.Compat;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// Reads an XMP packet with <see cref="XmlReader"/> (trim and Native AOT safe; it detects UTF-8 and UTF-16 itself).
/// DTDs are refused and no external resource is ever loaded.
/// </summary>
internal static class XmpParser
{
    /// <summary>The RDF namespace.</summary>
    private const string RdfNamespace = "http://www.w3.org/1999/02/22-rdf-syntax-ns#";

    /// <summary>The XML namespace-declaration namespace.</summary>
    private const string XmlnsNamespace = "http://www.w3.org/2000/xmlns/";

    /// <summary>The XML namespace.</summary>
    private const string XmlNamespace = "http://www.w3.org/XML/1998/namespace";

    /// <summary>The depth of an item inside a property's array container.</summary>
    private const int ItemDepth = 2;

    /// <summary>The depth of the text inside an item.</summary>
    private const int ItemTextDepth = 3;

    /// <summary>The language that marks the default entry of a language alternative.</summary>
    private const string DefaultLanguage = "x-default";

    /// <summary>The reader settings: no DTDs, no resolver, no whitespace or comments.</summary>
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreWhitespace = true,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        MaxCharactersInDocument = PdfLimits.MaxDecodedLength,
    };

    /// <summary>Parses a packet; damage ends the read but keeps what was found.</summary>
    /// <param name="packet">The decoded packet.</param>
    /// <returns>The metadata.</returns>
    internal static XmpMetadata Parse(byte[] packet)
    {
        var properties = new Dictionary<string, string[]>(StringComparer.Ordinal);
        var wellFormed = true;
        try
        {
            using var stream = MemoryStreams.OpenRead(packet);
            using var reader = XmlReader.Create(stream, Settings);
            Walk(reader, properties);
        }
        catch (XmlException)
        {
            wellFormed = false;
        }

        return new(packet, properties, wellFormed);
    }

    /// <summary>Walks the document, reading the properties of every <c>rdf:Description</c>.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="properties">The properties found.</param>
    private static void Walk(XmlReader reader, Dictionary<string, string[]> properties)
    {
        var descriptionDepth = -1;
        while (reader.Read())
        {
            if (reader.NodeType == XmlNodeType.Element && IsRdf(reader, "Description"))
            {
                ReadAttributes(reader, properties);
                descriptionDepth = reader.IsEmptyElement ? -1 : reader.Depth;
            }
            else if (reader.NodeType == XmlNodeType.Element && descriptionDepth >= 0 && reader.Depth == descriptionDepth + 1)
            {
                ReadProperty(reader, properties);
            }
            else if (reader.NodeType == XmlNodeType.EndElement && reader.Depth == descriptionDepth)
            {
                descriptionDepth = -1;
            }
        }
    }

    /// <summary>Reads the property attributes of an <c>rdf:Description</c>.</summary>
    /// <param name="reader">The reader, on the element.</param>
    /// <param name="properties">The properties found.</param>
    private static void ReadAttributes(XmlReader reader, Dictionary<string, string[]> properties)
    {
        if (!reader.MoveToFirstAttribute())
        {
            return;
        }

        do
        {
            if (reader.NamespaceURI is XmlnsNamespace or XmlNamespace or RdfNamespace || reader.NamespaceURI.Length == 0)
            {
                continue;
            }

            properties[XmpMetadata.Key(reader.NamespaceURI, reader.LocalName)] = [reader.Value];
        }
        while (reader.MoveToNextAttribute());

        _ = reader.MoveToElement();
    }

    /// <summary>Reads one property element: simple text, or the items of an array.</summary>
    /// <param name="reader">The reader, on the property element.</param>
    /// <param name="properties">The properties found.</param>
    private static void ReadProperty(XmlReader reader, Dictionary<string, string[]> properties)
    {
        var key = XmpMetadata.Key(reader.NamespaceURI, reader.LocalName);
        var isEmpty = reader.IsEmptyElement;
        var items = new List<string>();
        var direct = new StringBuilder();
        if (!isEmpty)
        {
            using var subtree = reader.ReadSubtree();
            ReadContent(subtree, items, direct);
        }

        if (items.Count == 0 && direct.ToString().Trim() is { Length: > 0 } text)
        {
            items.Add(text);
        }

        if (items.Count > 0)
        {
            properties[key] = [.. items];
        }
    }

    /// <summary>Reads a property's content, collecting array items and direct text.</summary>
    /// <param name="reader">A subtree reader on the property element.</param>
    /// <param name="items">The array items found.</param>
    /// <param name="direct">The text directly inside the property.</param>
    private static void ReadContent(XmlReader reader, List<string> items, StringBuilder direct)
    {
        StringBuilder? itemText = null;
        string? language = null;
        while (reader.Read())
        {
            switch (reader.NodeType)
            {
                case XmlNodeType.Element when reader.Depth == ItemDepth && IsRdf(reader, "li"):
                {
                    itemText = new();
                    language = reader.GetAttribute("lang", XmlNamespace);
                    if (reader.IsEmptyElement)
                    {
                        AddItem(items, itemText, language);
                        itemText = null;
                    }

                    break;
                }

                case XmlNodeType.Text or XmlNodeType.CDATA or XmlNodeType.SignificantWhitespace:
                {
                    AppendText(reader, direct, itemText);
                    break;
                }

                case XmlNodeType.EndElement when reader.Depth == ItemDepth && itemText is not null:
                {
                    AddItem(items, itemText, language);
                    itemText = null;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }
    }

    /// <summary>Appends a text node to the property's direct text or to the item it is in.</summary>
    /// <param name="reader">The reader, on the text.</param>
    /// <param name="direct">The direct text.</param>
    /// <param name="itemText">The current item's text, or null.</param>
    private static void AppendText(XmlReader reader, StringBuilder direct, StringBuilder? itemText)
    {
        if (reader.Depth == 1)
        {
            _ = direct.Append(reader.Value);
        }
        else if (reader.Depth == ItemTextDepth)
        {
            _ = itemText?.Append(reader.Value);
        }
    }

    /// <summary>Adds an item, placing a default-language entry first.</summary>
    /// <param name="items">The items.</param>
    /// <param name="text">The item's text.</param>
    /// <param name="language">The item's language, or null.</param>
    private static void AddItem(List<string> items, StringBuilder text, string? language)
    {
        if (string.Equals(language, DefaultLanguage, StringComparison.Ordinal))
        {
            items.Insert(0, text.ToString());
        }
        else
        {
            items.Add(text.ToString());
        }
    }

    /// <summary>Determines whether the reader is on an RDF element.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="localName">The local name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsRdf(XmlReader reader, string localName) =>
        string.Equals(reader.NamespaceURI, RdfNamespace, StringComparison.Ordinal) && string.Equals(reader.LocalName, localName, StringComparison.Ordinal);
}
