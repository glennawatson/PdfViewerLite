// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <content>Beveled and inset borders.</content>
internal static partial class FormAppearance
{
    /// <summary>The grey of the top and left of an inset border.</summary>
    private const float InsetLightGray = 0.5F;

    /// <summary>The grey of the bottom and right of an inset border.</summary>
    private const float InsetDarkGray = 0.75F;

    /// <summary>The grey of the top and left of a beveled border.</summary>
    private const float BevelLightGray = 1;

    /// <summary>
    /// Draws a beveled or inset border as PDFium draws it: a ring of the border colour as wide as half the border, inside it a
    /// lit L on the top and left and a shaded L on the bottom and right. A beveled border shades with half the background colour
    /// and has no shade without a background; an inset border uses fixed greys.
    /// </summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame; its border width is already twice the width the widget asks for.</param>
    /// <param name="border">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="background">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    internal static void PaintBevel(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? border, PdfArray? background)
    {
        var half = frame.Border.Width * Half;
        var width = frame.Width;
        var height = frame.Height;
        var inner = half + half;
        var beveled = frame.Border.Style == FormBorderStyle.Beveled;

        builder.SaveState();
        builder.SetFillGray(beveled ? BevelLightGray : InsetLightGray);
        FillCorners(ref builder, [half, half, half, height - half, width - half, height - half, width - inner, height - inner, inner, height - inner, inner, inner]);
        if (!beveled)
        {
            builder.SetFillGray(InsetDarkGray);
        }

        if (!beveled || (background is not null && TryWriteHalvedFill(ref builder, background)))
        {
            FillCorners(ref builder, [width - half, height - half, width - half, half, half, half, inner, inner, width - inner, inner, width - inner, height - inner]);
        }

        if (border is not null && TryWriteColor(ref builder, border, false))
        {
            builder.Rectangle(0, 0, width, height);
            builder.Rectangle(half, half, width - inner, height - inner);
            builder.FillEvenOdd();
        }

        builder.RestoreState();
    }

    /// <summary>Fills a polygon given as x, y pairs.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="points">The corners as x, y pairs.</param>
    private static void FillCorners(ref PdfContentBuilder builder, float[] points)
    {
        builder.MoveTo(points[0], points[1]);
        for (var i = CornerStride; i + 1 < points.Length; i += CornerStride)
        {
            builder.LineTo(points[i], points[i + 1]);
        }

        builder.ClosePath();
        builder.Fill();
    }

    /// <summary>Sets the fill colour to half of a colour: every component divided by two, which shades it.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The components: one for gray, three for RGB, four for CMYK.</param>
    /// <returns><see langword="false"/> when the array is empty or has another length, meaning no colour.</returns>
    private static bool TryWriteHalvedFill(ref PdfContentBuilder builder, PdfArray color)
    {
        switch (color.Count)
        {
            case GrayComponents:
            {
                builder.SetFillGray(color.GetSingle(0) * Half);
                return true;
            }

            case RgbComponents:
            {
                builder.SetFillRgb(color.GetSingle(0) * Half, color.GetSingle(1) * Half, color.GetSingle(BlueSlot) * Half);
                return true;
            }

            case CmykComponents:
            {
                builder.SetFillCmyk(color.GetSingle(0) * Half, color.GetSingle(1) * Half, color.GetSingle(BlueSlot) * Half, color.GetSingle(BlackSlot) * Half);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
