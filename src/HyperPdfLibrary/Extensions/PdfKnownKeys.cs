// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Extensions;

/// <summary>The keys ISO 32000-2 defines for the dictionaries that <see cref="PdfEntryOwner"/> names.</summary>
public static class PdfKnownKeys
{
    /// <summary>Gets the catalog keys (ISO 32000-2 Table 29).</summary>
    public static IReadOnlyList<string> Catalog { get; } =
    [
        "Type", "Version", "Extensions", "Pages", "PageLabels", "Names", "Dests", "ViewerPreferences", "PageLayout", "PageMode",
        "Outlines", "Threads", "OpenAction", "AA", "URI", "AcroForm", "Metadata", "StructTreeRoot", "MarkInfo", "Lang",
        "SpiderInfo", "OutputIntents", "PieceInfo", "OCProperties", "Perms", "Legal", "Requirements", "Collection",
        "NeedsRendering", "DSS", "AF", "DPartRoot",
    ];

    /// <summary>Gets the page keys (ISO 32000-2 Table 31).</summary>
    public static IReadOnlyList<string> Page { get; } =
    [
        "Type", "Parent", "LastModified", "Resources", "MediaBox", "CropBox", "BleedBox", "TrimBox", "ArtBox", "BoxColorInfo",
        "Contents", "Rotate", "Group", "Thumb", "B", "Dur", "Trans", "Annots", "AA", "Metadata", "PieceInfo", "StructParents",
        "ID", "PZ", "SeparationInfo", "Tabs", "TemplateInstantiated", "PresSteps", "UserUnit", "VP", "AF", "OutputIntents", "DPart",
    ];
}
