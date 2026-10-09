// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>The page attributes a page tree node passes down to its kids.</summary>
/// <param name="Resources">The resources.</param>
/// <param name="MediaBox">The media box.</param>
/// <param name="CropBox">The crop box.</param>
/// <param name="Rotate">The rotation.</param>
internal readonly record struct InheritedAttributes(PdfDictionary? Resources, PdfRectangle? MediaBox, PdfRectangle? CropBox, int Rotate)
{
    /// <summary>Applies a node's own attributes over the inherited ones.</summary>
    /// <param name="node">The page tree node.</param>
    /// <returns>The attributes its kids inherit.</returns>
    internal InheritedAttributes With(PdfDictionary node)
    {
        var rotate = node.Get(KnownName.Rotate);
        return new(
            node.GetDictionary(KnownName.Resources) ?? Resources,
            PdfPage.ReadBox(node, KnownName.MediaBox) ?? MediaBox,
            PdfPage.ReadBox(node, KnownName.CropBox) ?? CropBox,
            rotate.IsNumber ? rotate.AsInt32() : Rotate);
    }
}
