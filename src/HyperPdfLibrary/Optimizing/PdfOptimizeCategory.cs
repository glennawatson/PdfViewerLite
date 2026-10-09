// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The kind of change an optimisation makes, used to group savings, actions and skipped items in the report.</summary>
public enum PdfOptimizeCategory
{
    /// <summary>Changes to how the file is laid out: object streams, the cross-reference stream and dense numbering.</summary>
    Layout = 0,

    /// <summary>Images downsampled or re-encoded.</summary>
    Images = 1,

    /// <summary>Streams compressed or recompressed without changing their content.</summary>
    Streams = 2,

    /// <summary>Identical objects merged into one.</summary>
    Duplicates = 3,

    /// <summary>Objects no page or catalog entry reaches, dropped.</summary>
    UnusedObjects = 4,

    /// <summary>Embedded fonts cut down to the glyphs the pages use.</summary>
    Fonts = 5,

    /// <summary>Thumbnails, private application data and unused resources removed.</summary>
    Cleanup = 6,

    /// <summary>Language, title and tagging entries filled in, or a structure tree inferred.</summary>
    Accessibility = 7,

    /// <summary>Invisible text layers added to image-only pages.</summary>
    TextLayer = 8,

    /// <summary>Signature, encryption and PDF/A checks that limit what may change.</summary>
    Safety = 9,
}
