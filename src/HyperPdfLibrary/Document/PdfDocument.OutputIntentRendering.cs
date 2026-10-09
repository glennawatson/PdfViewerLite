// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Document;

/// <content>The render cache used when device colours convert through the output intent profile.</content>
public sealed partial class PdfDocument
{
    /// <summary>The claim state of a document whose XMP has no <c>pdfaid:part</c>.</summary>
    private const int NoClaim = 1;

    /// <summary>The claim state of a document whose XMP has a <c>pdfaid:part</c>.</summary>
    private const int Claimed = 2;

    /// <summary>The render cache for output intent rendering, or <see langword="null"/> when the document has no usable intent.</summary>
    private PdfRenderCache? _intentRenderCache;

    /// <summary>Whether <see cref="_intentRenderCache"/> has been decided.</summary>
    private int _intentDecided;

    /// <summary>Whether the document claims PDF/A: 0 not read yet, 1 no claim, 2 a claim.</summary>
    private int _pdfAClaimState;

    /// <summary>Gets a value indicating whether the catalog's XMP holds a <c>pdfaid:part</c>. The answer is read once.</summary>
    internal bool ClaimsPdfA
    {
        get
        {
            var state = Volatile.Read(ref _pdfAClaimState);
            if (state == 0)
            {
                state = GetXmp()?.PdfAPart is null ? NoClaim : Claimed;
                Volatile.Write(ref _pdfAClaimState, state);
            }

            return state == Claimed;
        }
    }

    /// <summary>Gets the render cache that converts device colours through the output intent profile.</summary>
    /// <returns>The cache, or <see langword="null"/> when the document has no PDF/A or PDF/X output intent with a usable profile.</returns>
    internal PdfRenderCache? GetOutputIntentRenderCache()
    {
        if (Volatile.Read(ref _intentDecided) != 0)
        {
            return Volatile.Read(ref _intentRenderCache);
        }

        var colors = OutputIntentColors.Create(GetOutputIntents());
        if (!colors.IsEmpty)
        {
            _ = Interlocked.CompareExchange(ref _intentRenderCache, new(this, colors), null);
        }

        Volatile.Write(ref _intentDecided, 1);
        return Volatile.Read(ref _intentRenderCache);
    }
}
