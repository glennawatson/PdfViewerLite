// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>An XMP property the metadata editor writes.</summary>
/// <param name="Prefix">The prefix used when the packet does not already bind the namespace.</param>
/// <param name="Namespace">The namespace URI.</param>
/// <param name="LocalName">The property's local name.</param>
/// <param name="Shape">How the value is written.</param>
[DebuggerDisplay("XmpProperty: {Prefix}:{LocalName}")]
internal sealed record XmpProperty(string Prefix, string Namespace, string LocalName, XmpShape Shape)
{
    /// <summary>The Dublin Core namespace.</summary>
    private const string DublinCore = "http://purl.org/dc/elements/1.1/";

    /// <summary>The Adobe PDF namespace.</summary>
    private const string AdobePdf = "http://ns.adobe.com/pdf/1.3/";

    /// <summary>The XMP basic namespace.</summary>
    private const string XmpBasic = "http://ns.adobe.com/xap/1.0/";

    /// <summary>Gets dc:title.</summary>
    internal static XmpProperty Title { get; } = new("dc", DublinCore, "title", XmpShape.Alternative);

    /// <summary>Gets dc:creator.</summary>
    internal static XmpProperty Author { get; } = new("dc", DublinCore, "creator", XmpShape.Sequence);

    /// <summary>Gets dc:description.</summary>
    internal static XmpProperty Subject { get; } = new("dc", DublinCore, "description", XmpShape.Alternative);

    /// <summary>Gets pdf:Keywords.</summary>
    internal static XmpProperty Keywords { get; } = new("pdf", AdobePdf, nameof(Keywords), XmpShape.Simple);

    /// <summary>Gets pdf:Producer.</summary>
    internal static XmpProperty Producer { get; } = new("pdf", AdobePdf, nameof(Producer), XmpShape.Simple);

    /// <summary>Gets xmp:CreatorTool.</summary>
    internal static XmpProperty Creator { get; } = new("xmp", XmpBasic, "CreatorTool", XmpShape.Simple);

    /// <summary>Gets xmp:CreateDate.</summary>
    internal static XmpProperty Created { get; } = new("xmp", XmpBasic, "CreateDate", XmpShape.Simple);

    /// <summary>Gets xmp:ModifyDate.</summary>
    internal static XmpProperty Modified { get; } = new("xmp", XmpBasic, "ModifyDate", XmpShape.Simple);

    /// <summary>Gets xmp:MetadataDate.</summary>
    internal static XmpProperty MetadataDate { get; } = new("xmp", XmpBasic, nameof(MetadataDate), XmpShape.Simple);
}
