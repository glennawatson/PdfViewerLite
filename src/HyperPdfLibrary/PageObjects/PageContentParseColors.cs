// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

using HyperPdfLibrary.Content;

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.PageObjects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>Reads fill and stroke paint operators from content streams.</summary>
internal static class PageContentParseColors
{
    /// <summary>Handles <c>g</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillGray(PageContentParseState self, ref ContentReader reader) => self.State.Fill = PdfPaint.FromGray(reader.Number(0));

    /// <summary>Handles <c>G</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeGray(PageContentParseState self, ref ContentReader reader) => self.State.Stroke = PdfPaint.FromGray(reader.Number(0));

    /// <summary>Handles <c>rg</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillRgb(
        PageContentParseState self,
        ref ContentReader reader) =>
        self.State.Fill = PdfPaint.FromRgb(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand));

    /// <summary>Handles <c>RG</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeRgb(
        PageContentParseState self,
        ref ContentReader reader) =>
        self.State.Stroke = PdfPaint.FromRgb(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand));

    /// <summary>Handles <c>k</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillCmyk(
        PageContentParseState self,
        ref ContentReader reader) =>
        self.State.Fill = PdfPaint.FromCmyk(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand),
            reader.Number(PageContentParse.FourthOperand));

    /// <summary>Handles <c>K</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeCmyk(
        PageContentParseState self,
        ref ContentReader reader) =>
        self.State.Stroke = PdfPaint.FromCmyk(
            reader.Number(0),
            reader.Number(1),
            reader.Number(PageContentParse.ThirdOperand),
            reader.Number(PageContentParse.FourthOperand));

    /// <summary>Handles <c>cs</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillColorSpace(PageContentParseState self, ref ContentReader reader) => self.State.Fill = PdfPaint.Initial(reader.Operand(0).Name);

    /// <summary>Handles <c>CS</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeColorSpace(PageContentParseState self, ref ContentReader reader) => self.State.Stroke = PdfPaint.Initial(reader.Operand(0).Name);

    /// <summary>Handles <c>sc</c> and <c>scn</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetFillColor(PageContentParseState self, ref ContentReader reader) => self.State.Fill = PageContentParseColors.ReadColor(self.State.Fill, ref reader);

    /// <summary>Handles <c>SC</c> and <c>SCN</c>.</summary>
    /// <param name = "self">The parser.</param>
    /// <param name = "reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void OpSetStrokeColor(PageContentParseState self, ref ContentReader reader) => self.State.Stroke = PageContentParseColors.ReadColor(self.State.Stroke, ref reader);

    /// <summary>Reads the components and pattern name of a colour operator, keeping the current colour space.</summary>
    /// <param name = "current">The colour in force, which names the space.</param>
    /// <param name = "reader">The reader holding the operands.</param>
    /// <returns>The new colour.</returns>
    internal static PdfPaint ReadColor(PdfPaint current, ref ContentReader reader)
    {
        var count = 0;
        var pattern = default(PdfName);
        for (var i = 0; i < reader.OperandCount; i++)
        {
            var operand = reader.Operand(i);
            count += operand.Kind == ContentOperandKind.Number ? 1 : 0;
            pattern = operand.Kind == ContentOperandKind.Name ? operand.Name : pattern;
        }

        var components = new float[count];
        var next = 0;
        for (var i = 0; i < reader.OperandCount; i++)
        {
            if (reader.Operand(i).Kind != ContentOperandKind.Number)
            {
                continue;
            }

            components[next] = reader.Number(i);
            next++;
        }

        return new(current.ColorSpace, components, pattern);
    }
}
