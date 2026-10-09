// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>
/// The optional cleanup steps. None of them touches /Metadata, /StructTreeRoot, /OutputIntents, /AF, /AcroForm,
/// signatures or /OCProperties.
/// </summary>
[Flags]
public enum PdfCleanupItems
{
    /// <summary>No cleanup.</summary>
    None = 0,

    /// <summary>Removes page /Thumb images; viewers draw their own thumbnails.</summary>
    Thumbnails = 1 << 0,

    /// <summary>Removes /PieceInfo private application data from the catalog, pages and form XObjects.</summary>
    PieceInfo = 1 << 1,

    /// <summary>Removes page resource entries the page content never names.</summary>
    UnusedResources = 1 << 2,

    /// <summary>Removes empty page /Annots arrays.</summary>
    EmptyAnnotations = 1 << 3,

    /// <summary>Every cleanup step.</summary>
    All = Thumbnails | PieceInfo | UnusedResources | EmptyAnnotations,
}
