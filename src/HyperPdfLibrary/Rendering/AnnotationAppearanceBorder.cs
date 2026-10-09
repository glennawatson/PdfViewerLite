// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Reads and writes annotation border settings.</summary>
internal static class AnnotationAppearanceBorder
{
    /// <summary>The position of the width in a /Border array.</summary>
    private const int BorderWidthSlot = 2;

    /// <summary>The length of a /Border array that holds a dash array.</summary>
    private const int BorderWithDash = 4;

    /// <summary>The position of the dash array in a /Border array.</summary>
    private const int BorderDashSlot = 3;

    /// <summary>The most dash lengths PDFium writes.</summary>
    private const int MaxDashes = 10;

    /// <summary>Reads the border width as PDFium does: /BS /W, else the third entry of /Border, else 1.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The width.</returns>
    internal static float BorderWidth(PdfDictionary annotation)
    {
        if (annotation.GetDictionary(KnownName.BS) is { } style && style.ContainsKey(KnownName.W))
        {
            return style.GetSingle(KnownName.W);
        }

        return annotation.GetArray(KnownName.Border) is { Count: > BorderWidthSlot } border ? border.GetSingle(BorderWidthSlot) : 1;
    }

    /// <summary>Writes the dash pattern as PDFium does: /BS /D when the style is dashed, else the fourth entry of /Border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="annotation">The annotation.</param>
    internal static void WriteDash(ref PdfContentBuilder builder, PdfDictionary annotation)
    {
        var dashes = annotation.GetArray(KnownName.Border) is { Count: BorderWithDash } border ? border.GetArray(BorderDashSlot) : null;
        if (annotation.GetDictionary(KnownName.BS) is { } style && style.IsName(KnownName.S, KnownName.D))
        {
            dashes = style.GetArray(KnownName.D);
        }

        if (dashes is not { Count: > 0 })
        {
            return;
        }

        Span<float> lengths = stackalloc float[MaxDashes];
        var count = Math.Min(dashes.Count, MaxDashes);
        for (var i = 0; i < count; i++)
        {
            lengths[i] = dashes.GetSingle(i);
        }

        builder.SetDash(lengths[..count], 0);
    }
}
