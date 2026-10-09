// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <summary>Renders pages using document output intents.</summary>
public static class PdfDocumentOutputIntentRendering
{
    /// <summary>The claim state of a document whose XMP has no <c>pdfaid:part</c>.</summary>
    private const int NoClaim = 1;

    /// <summary>The claim state of a document whose XMP has a <c>pdfaid:part</c>.</summary>
    private const int Claimed = 2;

    /// <summary>Gets a value indicating whether the catalog's XMP holds a <c>pdfaid:part</c>. The answer is read once.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when metadata claims PDF/A conformance.</returns>
    internal static bool ClaimsPdfA(PdfDocument document)
    {
        var state = Volatile.Read(ref document.State.PdfAClaimState);
        if (state == 0)
        {
            state = PdfDocumentMetadata.GetXmp(document)?.PdfAPart is null ? PdfDocumentOutputIntentRendering.NoClaim : PdfDocumentOutputIntentRendering.Claimed;
            Volatile.Write(ref document.State.PdfAClaimState, state);
        }

        return state == PdfDocumentOutputIntentRendering.Claimed;
    }

    /// <summary>Gets the render cache that converts device colours through the output intent profile.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The cache, or <see langword="null"/> when the document has no PDF/A or PDF/X output intent with a usable profile.</returns>
    internal static PdfRenderCache? GetOutputIntentRenderCache(PdfDocument document)
    {
        if (Volatile.Read(ref document.State.IntentDecided) != 0)
        {
            return Volatile.Read(ref document.State.IntentRenderCache);
        }

        var colors = OutputIntentColors.Create(PdfDocumentCatalog.GetOutputIntents(document));
        if (!colors.IsEmpty)
        {
            _ = Interlocked.CompareExchange(ref document.State.IntentRenderCache, new(document, colors), null);
        }

        Volatile.Write(ref document.State.IntentDecided, 1);
        return Volatile.Read(ref document.State.IntentRenderCache);
    }
}
