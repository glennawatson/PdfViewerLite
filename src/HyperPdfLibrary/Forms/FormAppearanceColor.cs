// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Forms;

/// <summary>Writes widget fill and stroke colours.</summary>
internal static class FormAppearanceColor
{
    /// <summary>The number of operands of a gray colour.</summary>
    internal const int GrayComponents = 1;

    /// <summary>The number of operands of an RGB colour.</summary>
    internal const int RgbComponents = 3;

    /// <summary>The number of operands of a CMYK colour.</summary>
    internal const int CmykComponents = 4;

    /// <summary>The position of the third colour component, the blue of an RGB colour or the yellow of a CMYK colour.</summary>
    internal const int BlueSlot = 2;

    /// <summary>The position of the fourth colour component, the black of a CMYK colour.</summary>
    internal const int BlackSlot = 3;

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
