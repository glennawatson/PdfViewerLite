// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Redaction;

/// <summary>What applying redactions removed.</summary>
/// <param name="Pages">The number of pages that had redactions applied.</param>
/// <param name="Regions">The number of redacted areas applied.</param>
/// <param name="GlyphsRemoved">The text glyphs removed.</param>
/// <param name="ImagesRemoved">The images removed whole.</param>
/// <param name="ImagesBlanked">The images whose pixels under an area were blanked.</param>
/// <param name="PathsRemoved">The paths and shadings removed.</param>
/// <param name="AnnotationsRemoved">The annotations, links and widgets removed, not counting the redact annotations themselves.</param>
/// <param name="FormsRewritten">The form XObjects that were copied and cleaned.</param>
/// <param name="ResourcesRemoved">The unused resources dropped from changed pages.</param>
/// <param name="ToUnicodeEntriesRemoved">The /ToUnicode entries removed for codes no text uses any more.</param>
[DebuggerDisplay("PdfRedactionReport: {Regions} areas on {Pages} pages, {GlyphsRemoved} glyphs")]
public sealed record PdfRedactionReport(
    int Pages,
    int Regions,
    int GlyphsRemoved,
    int ImagesRemoved,
    int ImagesBlanked,
    int PathsRemoved,
    int AnnotationsRemoved,
    int FormsRewritten,
    int ResourcesRemoved,
    int ToUnicodeEntriesRemoved)
{
    /// <summary>Gets a report of nothing done.</summary>
    public static PdfRedactionReport Empty { get; } = new(0, 0, 0, 0, 0, 0, 0, 0, 0, 0);

    /// <summary>Gets a value indicating whether applying removed or changed anything.</summary>
    public bool IsEmpty => this == Empty;
}
