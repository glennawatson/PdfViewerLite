// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <content>The colour operators.</content>
internal sealed partial class PageContentParser
{
    /// <summary>Handles <c>g</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetFillGray(PageContentParser self, ref ContentReader reader) => self._state.Fill = PdfPaint.FromGray(reader.Number(0));

    /// <summary>Handles <c>G</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetStrokeGray(PageContentParser self, ref ContentReader reader) => self._state.Stroke = PdfPaint.FromGray(reader.Number(0));

    /// <summary>Handles <c>rg</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetFillRgb(PageContentParser self, ref ContentReader reader) =>
        self._state.Fill = PdfPaint.FromRgb(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand));

    /// <summary>Handles <c>RG</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetStrokeRgb(PageContentParser self, ref ContentReader reader) =>
        self._state.Stroke = PdfPaint.FromRgb(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand));

    /// <summary>Handles <c>k</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetFillCmyk(PageContentParser self, ref ContentReader reader) =>
        self._state.Fill = PdfPaint.FromCmyk(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand));

    /// <summary>Handles <c>K</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetStrokeCmyk(PageContentParser self, ref ContentReader reader) =>
        self._state.Stroke = PdfPaint.FromCmyk(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand));

    /// <summary>Handles <c>cs</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetFillColorSpace(PageContentParser self, ref ContentReader reader) => self._state.Fill = PdfPaint.Initial(reader.Operand(0).Name);

    /// <summary>Handles <c>CS</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetStrokeColorSpace(PageContentParser self, ref ContentReader reader) => self._state.Stroke = PdfPaint.Initial(reader.Operand(0).Name);

    /// <summary>Handles <c>sc</c> and <c>scn</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetFillColor(PageContentParser self, ref ContentReader reader) => self._state.Fill = ReadColor(self._state.Fill, ref reader);

    /// <summary>Handles <c>SC</c> and <c>SCN</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpSetStrokeColor(PageContentParser self, ref ContentReader reader) => self._state.Stroke = ReadColor(self._state.Stroke, ref reader);

    /// <summary>Reads the components and pattern name of a colour operator, keeping the current colour space.</summary>
    /// <param name="current">The colour in force, which names the space.</param>
    /// <param name="reader">The reader holding the operands.</param>
    /// <returns>The new colour.</returns>
    private static PdfPaint ReadColor(PdfPaint current, ref ContentReader reader)
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
