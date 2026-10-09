// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Redaction;

/// <summary>How redaction treats what lies under a redacted area, and what it cleans up afterwards.</summary>
/// <param name="Images">What happens to images the area touches.</param>
/// <param name="LineArt">What happens to paths and shadings the area touches.</param>
/// <param name="Annotations">What happens to annotations, links and form fields the area touches.</param>
/// <param name="RemoveInvisibleText">Whether invisible text (render mode 3, such as an OCR layer) under the area is removed too.</param>
/// <param name="DrawOverlay">Whether the area is painted with the redact annotation's fill colour, overlay text or overlay form.</param>
/// <param name="RemoveUnusedResources">Whether fonts, images and other resources the changed pages no longer use are dropped.</param>
/// <param name="PruneToUnicode">Whether the /ToUnicode maps of fonts lose the entries of codes that no text uses any more.</param>
/// <param name="ScrubMetadata">Whether the document information and XMP metadata are removed.</param>
/// <param name="Layout">How the new file is laid out; applying always writes a new file, never an incremental update.</param>
[DebuggerDisplay("PdfRedactionOptions: images {Images}, line art {LineArt}, annotations {Annotations}")]
public sealed record PdfRedactionOptions(
    PdfRedactionImageMode Images,
    PdfRedactionLineArtMode LineArt,
    PdfRedactionAnnotationMode Annotations,
    bool RemoveInvisibleText,
    bool DrawOverlay,
    bool RemoveUnusedResources,
    bool PruneToUnicode,
    bool ScrubMetadata,
    PdfCompactOptions Layout)
{
    /// <summary>
    /// Gets the safe defaults: blank image pixels, remove line art that is wholly covered, remove touched annotations and
    /// links, remove invisible text, draw the overlay, clean up resources and /ToUnicode maps, keep the metadata.
    /// </summary>
    public static PdfRedactionOptions Default { get; } = new(
        PdfRedactionImageMode.BlankPixels,
        PdfRedactionLineArtMode.RemoveCovered,
        PdfRedactionAnnotationMode.RemoveTouched,
        true,
        true,
        true,
        true,
        false,
        PdfCompactOptions.Default);
}
