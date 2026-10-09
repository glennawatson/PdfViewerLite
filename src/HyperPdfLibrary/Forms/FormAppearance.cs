// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Draws the appearance streams of form widgets: the background, the border and the field's content.</summary>
internal static partial class FormAppearance
{
    /// <summary>The number of operands of a gray colour.</summary>
    private const int GrayComponents = 1;

    /// <summary>The number of operands of an RGB colour.</summary>
    private const int RgbComponents = 3;

    /// <summary>The number of operands of a CMYK colour.</summary>
    private const int CmykComponents = 4;

    /// <summary>The position of the third colour component, the blue of an RGB colour or the yellow of a CMYK colour.</summary>
    private const int BlueSlot = 2;

    /// <summary>The position of the fourth colour component, the black of a CMYK colour.</summary>
    private const int BlackSlot = 3;

    /// <summary>Half, to centre a line on a path.</summary>
    private const float Half = 0.5F;

    /// <summary>The control point offset that makes four Bezier curves a circle.</summary>
    private const float CircleKappa = 0.5522847F;

    /// <summary>Creates the appearance of a text field, or of a combo box without a drop-down arrow.</summary>
    /// <param name="context">The widget.</param>
    /// <param name="display">The text to show.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateText(AppearanceContext context, string display)
    {
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var font = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var builder = default(PdfContentBuilder);
        try
        {
            Paint(ref builder, frame, context.Characteristics, false);
            WriteText(ref builder, context, frame, da, font, display);
            return Finish(ref builder, context, frame, font);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Creates the appearance of a list box.</summary>
    /// <param name="context">The widget.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream CreateList(AppearanceContext context)
    {
        var frame = FormFrame.Create(context);
        var da = context.DefaultAppearance;
        var font = FormFont.Find(context.Store, da.FontName, context.Widget, context.Form, context.PageResources, context.Fonts);
        var builder = default(PdfContentBuilder);
        try
        {
            Paint(ref builder, frame, context.Characteristics, false);
            WriteList(ref builder, context, frame, da, font);
            return Finish(ref builder, context, frame, font);
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes a colour operator for a colour array.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The components: one for gray, three for RGB, four for CMYK.</param>
    /// <param name="stroke">Whether to set the stroking colour instead of the filling colour.</param>
    /// <returns><see langword="false"/> when the array is empty or has another length, meaning no colour.</returns>
    internal static bool TryWriteColor(ref PdfContentBuilder builder, PdfArray color, bool stroke)
    {
        switch (color.Count)
        {
            case GrayComponents:
            {
                SetGray(ref builder, color.GetSingle(0), stroke);
                return true;
            }

            case RgbComponents:
            {
                SetRgb(ref builder, color, stroke);
                return true;
            }

            case CmykComponents:
            {
                SetCmyk(ref builder, color, stroke);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

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

    /// <summary>Wraps the drawn content in a Form XObject whose resources hold the font.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="context">The widget.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="font">The font, or <see langword="null"/> when the content uses none.</param>
    /// <returns>The Form XObject.</returns>
    internal static PdfStream Finish(ref PdfContentBuilder builder, AppearanceContext context, in FormFrame frame, FormFont? font)
    {
        PdfDictionary? resources = null;
        if (font is not null)
        {
            PdfDictionary fonts = new(context.Store);
            fonts.Set(context.Store.Names.Intern(font.Name), font.Resource);
            resources = new(context.Store);
            resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        }

        return builder.ToFormXObject(context.Store, new(0, 0, frame.Width, frame.Height), frame.GetMatrix(), resources);
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
        if (TryWriteColor(ref builder, color, false))
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
        if (TryWriteColor(ref builder, color, true))
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

    /// <summary>Sets a gray colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="gray">The gray level.</param>
    /// <param name="stroke">Whether to set the stroking colour.</param>
    private static void SetGray(ref PdfContentBuilder builder, float gray, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeGray(gray);
        }
        else
        {
            builder.SetFillGray(gray);
        }
    }

    /// <summary>Sets an RGB colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The three components.</param>
    /// <param name="stroke">Whether to set the stroking colour.</param>
    private static void SetRgb(ref PdfContentBuilder builder, PdfArray color, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeRgb(color.GetSingle(0), color.GetSingle(1), color.GetSingle(BlueSlot));
        }
        else
        {
            builder.SetFillRgb(color.GetSingle(0), color.GetSingle(1), color.GetSingle(BlueSlot));
        }
    }

    /// <summary>Sets a CMYK colour.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The four components.</param>
    /// <param name="stroke">Whether to set the stroking colour.</param>
    private static void SetCmyk(ref PdfContentBuilder builder, PdfArray color, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeCmyk(color.GetSingle(0), color.GetSingle(1), color.GetSingle(BlueSlot), color.GetSingle(BlackSlot));
        }
        else
        {
            builder.SetFillCmyk(color.GetSingle(0), color.GetSingle(1), color.GetSingle(BlueSlot), color.GetSingle(BlackSlot));
        }
    }
}
