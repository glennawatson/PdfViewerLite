// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Changes to a document's metadata. A <see langword="null"/> property is left as it is; an empty string removes the
/// entry. Each change is written to /Info and, when the document has one, to the matching XMP property.
/// </summary>
[DebuggerDisplay("PdfMetadataEdit: {Title}")]
public sealed record PdfMetadataEdit
{
    /// <summary>Gets the title (/Title, dc:title).</summary>
    public string? Title { get; init; }

    /// <summary>Gets the author (/Author, dc:creator).</summary>
    public string? Author { get; init; }

    /// <summary>Gets the subject (/Subject, dc:description).</summary>
    public string? Subject { get; init; }

    /// <summary>Gets the keywords (/Keywords, pdf:Keywords).</summary>
    public string? Keywords { get; init; }

    /// <summary>Gets the creating application (/Creator, xmp:CreatorTool).</summary>
    public string? Creator { get; init; }

    /// <summary>Gets the producing application (/Producer, pdf:Producer).</summary>
    public string? Producer { get; init; }

    /// <summary>Gets the creation date (/CreationDate, xmp:CreateDate).</summary>
    public DateTimeOffset? Created { get; init; }

    /// <summary>Gets the modification date (/ModDate, xmp:ModifyDate and xmp:MetadataDate).</summary>
    public DateTimeOffset? Modified { get; init; }
}
