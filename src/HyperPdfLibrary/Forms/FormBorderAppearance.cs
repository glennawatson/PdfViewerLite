// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Draws widget backgrounds and borders.</summary>
internal static class FormBorderAppearance
{
    /// <summary>Half, to centre a line on a path.</summary>
    private const float Half = 0.5F;

    /// <summary>The control point offset that makes four Bezier curves a circle.</summary>
    private const float CircleKappa = 0.5522847F;

    /// <summary>The numbers that make up a corner: x and y.</summary>
    private const int CornerStride = 2;

    /// <summary>The grey of the top and left of an inset border.</summary>
    private const float InsetLightGray = 0.5F;

    /// <summary>The grey of the bottom and right of an inset border.</summary>
    private const float InsetDarkGray = 0.75F;

    /// <summary>The grey of the top and left of a beveled border.</summary>
    private const float BevelLightGray = 1;

    /// <summary>Draws a circle or ellipse path with four curves.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="centerX">The centre's x.</param>
    /// <param name="centerY">The centre's y.</param>
    /// <param name="radiusX">The horizontal radius.</param>
    /// <param name="radiusY">The vertical radius.</param>
    internal static void Ellipse(ref PdfContentBuilder builder, float centerX, float centerY, float radiusX, float radiusY)
    {
        var kx = radiusX * CircleKappa;
        var ky = radiusY * CircleKappa;
        builder.MoveTo(centerX + radiusX, centerY);
        builder.CurveTo(centerX + radiusX, centerY + ky, centerX + kx, centerY + radiusY, centerX, centerY + radiusY);
        builder.CurveTo(centerX - kx, centerY + radiusY, centerX - radiusX, centerY + ky, centerX - radiusX, centerY);
        builder.CurveTo(centerX - radiusX, centerY - ky, centerX - kx, centerY - radiusY, centerX, centerY - radiusY);
        builder.CurveTo(centerX + kx, centerY - radiusY, centerX + radiusX, centerY - ky, centerX + radiusX, centerY);
        builder.ClosePath();
    }

    /// <summary>Draws the background and border.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="characteristics">The widget's <c>/MK</c>, or <see langword="null"/>.</param>
    /// <param name="circle">Whether the background and border are a circle, as a radio button's are.</param>
    internal static void Paint(ref PdfContentBuilder builder, in FormFrame frame, PdfDictionary? characteristics, bool circle)
    {
        var background = characteristics?.GetArray(KnownName.BG);
        PaintBackground(ref builder, frame, background, circle);
        if (frame.Border.Width <= 0)
        {
            return;
        }

        // A radio button's round bevel is drawn as a plain round border.
        if (!circle && frame.Border.Style is FormBorderStyle.Beveled or FormBorderStyle.Inset)
        {
            PaintBevel(ref builder, frame, characteristics?.GetArray(KnownName.BC), background);
            return;
        }

        PaintBorder(ref builder, frame, characteristics?.GetArray(KnownName.BC), circle);
    }

    /// <summary>Fills the background in the widget's background colour, if it has one.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="color">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    /// <param name="circle">Whether to fill an ellipse.</param>
    private static void PaintBackground(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? color, bool circle)
    {
        if (color is null)
        {
            return;
        }

        builder.SaveState();
        if (FormAppearanceColor.TryWriteColor(ref builder, color, false))
        {
            FillBackground(ref builder, frame, circle);
        }

        builder.RestoreState();
    }

    /// <summary>Strokes the border in the widget's border colour, if it has one.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="color">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="circle">Whether to stroke an ellipse.</param>
    private static void PaintBorder(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? color, bool circle)
    {
        if (color is null)
        {
            return;
        }

        builder.SaveState();
        if (FormAppearanceColor.TryWriteColor(ref builder, color, true))
        {
            StrokeBorder(ref builder, frame, circle);
        }

        builder.RestoreState();
    }

    /// <summary>Fills the drawing area with the colour already set.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="circle">Whether to fill an ellipse.</param>
    private static void FillBackground(ref PdfContentBuilder builder, in FormFrame frame, bool circle)
    {
        if (circle)
        {
            Ellipse(ref builder, frame.Width * Half, frame.Height * Half, frame.Width * Half, frame.Height * Half);
        }
        else
        {
            builder.Rectangle(0, 0, frame.Width, frame.Height);
        }

        builder.Fill();
    }

    /// <summary>Strokes the border with the colour already set.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="circle">Whether to stroke an ellipse.</param>
    private static void StrokeBorder(ref PdfContentBuilder builder, in FormFrame frame, bool circle)
    {
        var width = frame.Border.Width;
        builder.SetLineWidth(width);
        if (frame.Border.Style == FormBorderStyle.Dashed)
        {
            builder.SetDash([frame.Border.Dash], 0);
        }

        if (circle)
        {
            Ellipse(ref builder, frame.Width * Half, frame.Height * Half, (frame.Width - width) * Half, (frame.Height - width) * Half);
        }
        else if (frame.Border.Style == FormBorderStyle.Underline)
        {
            builder.MoveTo(0, width * Half);
            builder.LineTo(frame.Width, width * Half);
        }
        else
        {
            builder.Rectangle(width * Half, width * Half, frame.Width - width, frame.Height - width);
        }

        builder.Stroke();
    }

    /// <summary>
    /// Draws a beveled or inset border as PDFium draws it: a ring of the border colour as wide as half the border, inside it a
    /// lit L on the top and left and a shaded L on the bottom and right. A beveled border shades with half the background colour
    /// and has no shade without a background; an inset border uses fixed greys.
    /// </summary>
    /// <param name="builder">The content.</param>
    /// <param name="frame">The frame; its border width is already twice the width the widget asks for.</param>
    /// <param name="border">The <c>/BC</c> colour, or <see langword="null"/>.</param>
    /// <param name="background">The <c>/BG</c> colour, or <see langword="null"/>.</param>
    private static void PaintBevel(ref PdfContentBuilder builder, in FormFrame frame, PdfArray? border, PdfArray? background)
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

        if (border is not null && FormAppearanceColor.TryWriteColor(ref builder, border, false))
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
            case FormAppearanceColor.GrayComponents:
            {
                builder.SetFillGray(color.GetSingle(0) * Half);
                return true;
            }

            case FormAppearanceColor.RgbComponents:
            {
                builder.SetFillRgb(color.GetSingle(0) * Half, color.GetSingle(1) * Half, color.GetSingle(FormAppearanceColor.BlueSlot) * Half);
                return true;
            }

            case FormAppearanceColor.CmykComponents:
            {
                builder.SetFillCmyk(color.GetSingle(0) * Half, color.GetSingle(1) * Half, color.GetSingle(FormAppearanceColor.BlueSlot) * Half, color.GetSingle(FormAppearanceColor.BlackSlot) * Half);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }
}
