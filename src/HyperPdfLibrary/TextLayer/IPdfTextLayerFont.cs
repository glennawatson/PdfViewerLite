// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.TextLayer;

/// <summary>A font an invisible text layer is written in: how text becomes codes and how wide those codes are.</summary>
public interface IPdfTextLayerFont
{
    /// <summary>Gets the font dictionary, or a reference to it, for a page's <c>/Font</c> resources.</summary>
    PdfValue Resource { get; }

    /// <summary>Gets the height above the baseline, as a share of the font size.</summary>
    float Ascent { get; }

    /// <summary>Gets the depth below the baseline, as a positive share of the font size.</summary>
    float Descent { get; }

    /// <summary>Encodes text as the codes the font shows, leaving out characters the font cannot show.</summary>
    /// <param name="text">The text.</param>
    /// <param name="codes">Receives the codes; at least twice as long as the text.</param>
    /// <param name="extent">Receives how far the codes reach; a font without glyph boxes reports the advance as the ink.</param>
    /// <returns>The number of bytes written.</returns>
    int Encode(ReadOnlySpan<char> text, Span<byte> codes, out PdfTextExtent extent);
}
