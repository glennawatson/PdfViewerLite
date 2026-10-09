// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Redaction;

/// <summary>A redact annotation on a page: the areas to redact and how they will look.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="AnnotationIndex">The annotation's index in the page's <c>/Annots</c> array.</param>
/// <param name="Regions">The areas in user space: the bounds of each quadrilateral, or the annotation's rectangle.</param>
/// <param name="Appearance">How the areas look after they are applied.</param>
[DebuggerDisplay("PdfRedaction: page {PageIndex} annotation {AnnotationIndex}, {Regions.Length} areas")]
public sealed record PdfRedaction(int PageIndex, int AnnotationIndex, PdfRectangle[] Regions, PdfRedactionAppearance Appearance)
{
    /// <summary>Gets the smallest rectangle around every area.</summary>
    public PdfRectangle Bounds
    {
        get
        {
            if (Regions.Length == 0)
            {
                return default;
            }

            var bounds = Regions[0];
            for (var i = 1; i < Regions.Length; i++)
            {
                bounds = bounds.Union(Regions[i]);
            }

            return bounds;
        }
    }
}
