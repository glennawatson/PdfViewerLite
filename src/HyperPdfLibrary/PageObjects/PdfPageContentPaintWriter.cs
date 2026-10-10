// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Writes device, named and pattern paints.</summary>
public static class PdfPageContentPaintWriter
{
    /// <summary>Sets a grey colour.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "gray">The level.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void PaintGray(ref PdfContentBuilder builder, float gray, bool stroke)
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
    /// <param name = "builder">The output.</param>
    /// <param name = "c">The three components.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void PaintRgb(ref PdfContentBuilder builder, float[] c, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeRgb(c[0], c[1], c[PdfPageContentWriter.RgbComponents - 1]);
        }
        else
        {
            builder.SetFillRgb(c[0], c[1], c[PdfPageContentWriter.RgbComponents - 1]);
        }
    }

    /// <summary>Sets a CMYK colour.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "c">The four components.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void PaintCmyk(ref PdfContentBuilder builder, float[] c, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeCmyk(c[0], c[1], c[PdfPageContentWriter.RgbComponents - 1], c[PdfPageContentWriter.CmykComponents - 1]);
        }
        else
        {
            builder.SetFillCmyk(c[0], c[1], c[PdfPageContentWriter.RgbComponents - 1], c[PdfPageContentWriter.CmykComponents - 1]);
        }
    }

    /// <summary>Sets the components of a colour in a named colour space.</summary>
    /// <param name = "builder">The output.</param>
    /// <param name = "components">The components.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void PaintComponents(ref PdfContentBuilder builder, float[] components, bool stroke)
    {
        if (stroke)
        {
            builder.SetStrokeColorN(components);
        }
        else
        {
            builder.SetFillColorN(components);
        }
    }

    /// <summary>Writes the colour operators that set a colour.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "paint">The colour.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void WritePaint(PdfPageContent state, ref PdfContentBuilder builder, PdfPaint paint, bool stroke)
    {
        var components = paint.Components;
        switch (paint.ColorSpace.ToKnownName())
        {
            case KnownName.DeviceGray when components.Length == 1 && paint.Pattern.IsNone:
                {
                    PdfPageContentPaintWriter.PaintGray(ref builder, components[0], stroke);
                    break;
                }

            case KnownName.DeviceRGB when components.Length == PdfPageContentWriter.RgbComponents && paint.Pattern.IsNone:
                {
                    PdfPageContentPaintWriter.PaintRgb(ref builder, components, stroke);
                    break;
                }

            case KnownName.DeviceCMYK when components.Length == PdfPageContentWriter.CmykComponents && paint.Pattern.IsNone:
                {
                    PdfPageContentPaintWriter.PaintCmyk(ref builder, components, stroke);
                    break;
                }

            default:
                {
                    PdfPageContentPaintWriter.PaintNamed(state, ref builder, paint, stroke);
                    break;
                }
        }
    }

    /// <summary>Sets a colour in a named colour space, with an optional pattern.</summary>
    /// <param name = "state">The owned parse or content state.</param>
    /// <param name = "builder">The output.</param>
    /// <param name = "paint">The colour.</param>
    /// <param name = "stroke">Whether it is the stroke colour.</param>
    internal static void PaintNamed(PdfPageContent state, ref PdfContentBuilder builder, PdfPaint paint, bool stroke)
    {
        var names = state.Document.Objects.Names;
        var space = names.GetSpelling(paint.ColorSpace);
        if (stroke)
        {
            builder.SetStrokeColorSpace(space);
        }
        else
        {
            builder.SetFillColorSpace(space);
        }

        if (paint.Pattern.IsNone)
        {
            if (paint.Components.Length > 0)
            {
                PdfPageContentPaintWriter.PaintComponents(ref builder, paint.Components, stroke);
            }

            return;
        }

        var pattern = names.GetSpelling(paint.Pattern);
        if (stroke)
        {
            builder.SetStrokeColorN(paint.Components, pattern);
        }
        else
        {
            builder.SetFillColorN(paint.Components, pattern);
        }
    }
}
