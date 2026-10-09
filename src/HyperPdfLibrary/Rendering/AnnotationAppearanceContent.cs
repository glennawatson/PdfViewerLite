// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Rendering;

/// <summary>Writes content shared by generated annotation appearances.</summary>
internal static class AnnotationAppearanceContent
{
    /// <summary>Writes a colour from an annotation entry, or a default when the entry is missing.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The colour array, or null.</param>
    /// <param name="fallbackRgb">The default as 0xRRGGBB, or null to write nothing.</param>
    /// <param name="stroke">Whether to set the stroke colour.</param>
    internal static void WriteColor(ref PdfContentBuilder builder, PdfArray? color, uint? fallbackRgb, bool stroke)
    {
        if (color is not null)
        {
            _ = FormAppearanceColor.TryWriteColor(ref builder, color, stroke);
            return;
        }

        if (fallbackRgb is not { } rgb)
        {
            return;
        }

        const int redShift = 16;
        const int greenShift = 8;
        const float channel = 255;
        var red = ((rgb >> redShift) & byte.MaxValue) / channel;
        var green = ((rgb >> greenShift) & byte.MaxValue) / channel;
        var blue = (rgb & byte.MaxValue) / channel;
        if (stroke)
        {
            builder.SetStrokeRgb(red, green, blue);
        }
        else
        {
            builder.SetFillRgb(red, green, blue);
        }
    }

    /// <summary>Starts an appearance by selecting its graphics state, as every PDFium appearance does.</summary>
    /// <param name="builder">The content.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void Begin(ref PdfContentBuilder builder) => builder.SetGraphicsState("GS"u8);
}
