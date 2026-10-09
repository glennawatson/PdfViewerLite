// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Metadata;

/// <summary>
/// An XMP packet read from a <c>/Metadata</c> stream. Every simple property and array item is kept in
/// <see cref="Properties"/>, keyed <c>"{namespace-uri}local-name"</c>; the common ones also have named accessors.
/// </summary>
/// <param name="RawPacket">The decoded packet, exactly as stored.</param>
/// <param name="Properties">The property values by key. Arrays list their items in order; a language alternative lists its default first.</param>
/// <param name="IsWellFormed">Whether the packet parsed as XML to the end. A damaged packet keeps the properties read before the damage.</param>
[DebuggerDisplay("XmpMetadata: {Title}")]
public sealed record XmpMetadata(byte[] RawPacket, IReadOnlyDictionary<string, string[]> Properties, bool IsWellFormed)
{
    /// <summary>The Dublin Core namespace.</summary>
    public static readonly string DublinCoreNamespace = "http://purl.org/dc/elements/1.1/";

    /// <summary>The Adobe PDF namespace.</summary>
    public static readonly string PdfNamespace = "http://ns.adobe.com/pdf/1.3/";

    /// <summary>The XMP basic namespace.</summary>
    public static readonly string XmpNamespace = "http://ns.adobe.com/xap/1.0/";

    /// <summary>The PDF/A identification namespace.</summary>
    public static readonly string PdfAIdNamespace = "http://www.aiim.org/pdfa/ns/id/";

    /// <summary>The PDF/UA identification namespace.</summary>
    public static readonly string PdfUaIdNamespace = "http://www.aiim.org/pdfua/ns/id/";

    /// <summary>Gets the title (the default language).</summary>
    public string? Title => GetValue(DublinCoreNamespace, "title");

    /// <summary>Gets the creators (authors).</summary>
    public string[] Creators => GetValues(DublinCoreNamespace, "creator");

    /// <summary>Gets the description (the default language).</summary>
    public string? Description => GetValue(DublinCoreNamespace, "description");

    /// <summary>Gets the subjects (keywords as a list).</summary>
    public string[] Subjects => GetValues(DublinCoreNamespace, "subject");

    /// <summary>Gets the producing application (<c>pdf:Producer</c>).</summary>
    public string? Producer => GetValue(PdfNamespace, nameof(Producer));

    /// <summary>Gets the keywords string (<c>pdf:Keywords</c>).</summary>
    public string? Keywords => GetValue(PdfNamespace, nameof(Keywords));

    /// <summary>Gets the PDF version written in the packet (<c>pdf:PDFVersion</c>).</summary>
    public string? PdfVersion => GetValue(PdfNamespace, "PDFVersion");

    /// <summary>Gets the creating tool (<c>xmp:CreatorTool</c>).</summary>
    public string? CreatorTool => GetValue(XmpNamespace, nameof(CreatorTool));

    /// <summary>Gets the creation date (<c>xmp:CreateDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? CreateDate => GetDate(nameof(CreateDate));

    /// <summary>Gets the modification date (<c>xmp:ModifyDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? ModifyDate => GetDate(nameof(ModifyDate));

    /// <summary>Gets the date the metadata last changed (<c>xmp:MetadataDate</c>), or null when missing or not a date.</summary>
    public DateTimeOffset? MetadataDate => GetDate(nameof(MetadataDate));

    /// <summary>Gets the PDF/A part (<c>pdfaid:part</c>), or null.</summary>
    public int? PdfAPart => GetInt(PdfAIdNamespace, "part");

    /// <summary>Gets the PDF/A conformance level (<c>pdfaid:conformance</c>), for example "B", or null.</summary>
    public string? PdfAConformance => GetValue(PdfAIdNamespace, "conformance");

    /// <summary>Gets the PDF/A revision year (<c>pdfaid:rev</c>), or null.</summary>
    public int? PdfARevision => GetInt(PdfAIdNamespace, "rev");

    /// <summary>Gets the PDF/UA part (<c>pdfuaid:part</c>), or null.</summary>
    public int? PdfUaPart => GetInt(PdfUaIdNamespace, "part");

    /// <summary>Builds a property key.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns>The key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static string Key(string namespaceUri, string localName) => $"{{{namespaceUri}}}{localName}";

    /// <summary>Gets the first value of a property.</summary>
    /// <param name="namespaceUri">The property's namespace URI.</param>
    /// <param name="localName">The property's local name.</param>
    /// <returns>The value, or null when missing.</returns>
    public string? GetValue(string namespaceUri, string localName) =>
        Properties.TryGetValue(Key(namespaceUri, localName), out var values) && values.Length > 0 ? values[0] : null;

    /// <summary>Gets all values of a property.</summary>
    /// <param name="namespaceUri">The property's namespace URI.</param>
    /// <param name="localName">The property's local name.</param>
    /// <returns>The values; empty when missing.</returns>
    public string[] GetValues(string namespaceUri, string localName) =>
        Properties.TryGetValue(Key(namespaceUri, localName), out var values) ? values : [];

    /// <summary>Reads an integer property.</summary>
    /// <param name="namespaceUri">The namespace URI.</param>
    /// <param name="localName">The local name.</param>
    /// <returns>The integer, or null.</returns>
    private int? GetInt(string namespaceUri, string localName) =>
        int.TryParse(GetValue(namespaceUri, localName), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value) ? value : null;

    /// <summary>Reads an <c>xmp:</c> date property.</summary>
    /// <param name="localName">The local name.</param>
    /// <returns>The date, or null.</returns>
    private DateTimeOffset? GetDate(string localName) =>
        DateTimeOffset.TryParse(GetValue(XmpNamespace, localName), CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var value) ? value : null;
}
