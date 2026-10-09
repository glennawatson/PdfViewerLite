// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Content;

/// <summary>
/// Recognises content stream operators. Every operator is one to three bytes, so its bytes and length pack into one
/// integer key, looked up in a small open-addressed table built once: one multiply, a shift and usually one compare.
/// </summary>
internal static class OperatorTable
{
    /// <summary>The longest operator.</summary>
    private const int MaxLength = 3;

    /// <summary>The bits of the table index.</summary>
    private const int IndexBits = 9;

    /// <summary>The table size; a power of two well above the operator count, so probes are short.</summary>
    private const int TableSize = 1 << IndexBits;

    /// <summary>The Fibonacci hashing multiplier.</summary>
    private const uint HashMultiplier = 2_654_435_769;

    /// <summary>The shift that keeps the top <see cref="IndexBits"/> bits of the hash.</summary>
    private const int HashShift = 32 - IndexBits;

    /// <summary>The shift of the length in a key.</summary>
    private const int LengthShift = 24;

    /// <summary>The bits per byte in a key.</summary>
    private const int ByteBits = 8;

    /// <summary>The packed keys, or zero for an empty slot.</summary>
    private static readonly uint[] Keys = new uint[TableSize];

    /// <summary>The operators, parallel to <see cref="Keys"/>.</summary>
    private static readonly ContentOperator[] Operators = new ContentOperator[TableSize];

    /// <summary>Initializes static members of the <see cref="OperatorTable"/> class.</summary>
    static OperatorTable()
    {
        foreach (var (spelling, op) in Spellings())
        {
            var key = Pack(Encoding.UTF8.GetBytes(spelling));
            var slot = Slot(key);
            while (Keys[slot] != 0)
            {
                slot = (slot + 1) & (TableSize - 1);
            }

            Keys[slot] = key;
            Operators[slot] = op;
        }
    }

    /// <summary>Recognises an operator.</summary>
    /// <param name="lexeme">The operator's bytes.</param>
    /// <returns>The operator, or <see cref="ContentOperator.Unknown"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static ContentOperator Lookup(ReadOnlySpan<byte> lexeme)
    {
        if (lexeme.IsEmpty || lexeme.Length > MaxLength)
        {
            return ContentOperator.Unknown;
        }

        var key = Pack(lexeme);
        for (var slot = Slot(key); Keys[slot] != 0; slot = (slot + 1) & (TableSize - 1))
        {
            if (Keys[slot] == key)
            {
                return Operators[slot];
            }
        }

        return ContentOperator.Unknown;
    }

    /// <summary>Packs up to three bytes and the length into a key.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The key, never zero.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(ReadOnlySpan<byte> bytes)
    {
        var key = (uint)bytes.Length << LengthShift;
        for (var i = 0; i < bytes.Length; i++)
        {
            key |= (uint)bytes[i] << (i * ByteBits);
        }

        return key;
    }

    /// <summary>Gets the home slot of a key.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The slot.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static int Slot(uint key) => (int)((key * HashMultiplier) >> HashShift);

    /// <summary>Lists every operator spelling.</summary>
    /// <returns>The spellings and operators.</returns>
    private static OperatorSpelling[] Spellings() =>
    [
        new("b", ContentOperator.CloseFillStroke), new("B", ContentOperator.FillStroke), new("b*", ContentOperator.CloseFillStrokeEvenOdd),
        new("B*", ContentOperator.FillStrokeEvenOdd), new("BDC", ContentOperator.BeginMarkedContentProperties), new("BI", ContentOperator.BeginInlineImage),
        new("BMC", ContentOperator.BeginMarkedContent), new("BT", ContentOperator.BeginText), new("BX", ContentOperator.BeginCompatibility),
        new("c", ContentOperator.CurveTo), new("cm", ContentOperator.ConcatMatrix), new("CS", ContentOperator.SetStrokeColorSpace),
        new("cs", ContentOperator.SetFillColorSpace), new("d", ContentOperator.SetDash), new("d0", ContentOperator.SetGlyphWidth),
        new("d1", ContentOperator.SetGlyphWidthAndBounds), new("Do", ContentOperator.PaintXObject), new("DP", ContentOperator.MarkPointProperties),
        new("EI", ContentOperator.EndInlineImage), new("EMC", ContentOperator.EndMarkedContent), new("ET", ContentOperator.EndText),
        new("EX", ContentOperator.EndCompatibility), new("f", ContentOperator.Fill), new("F", ContentOperator.Fill), new("f*", ContentOperator.FillEvenOdd),
        new("G", ContentOperator.SetStrokeGray), new("g", ContentOperator.SetFillGray), new("gs", ContentOperator.SetGraphicsState),
        new("h", ContentOperator.ClosePath), new("i", ContentOperator.SetFlatness), new("ID", ContentOperator.InlineImageData),
        new("j", ContentOperator.SetLineJoin), new("J", ContentOperator.SetLineCap), new("K", ContentOperator.SetStrokeCmyk),
        new("k", ContentOperator.SetFillCmyk), new("l", ContentOperator.LineTo), new("m", ContentOperator.MoveTo), new("M", ContentOperator.SetMiterLimit),
        new("MP", ContentOperator.MarkPoint), new("n", ContentOperator.EndPath), new("q", ContentOperator.Save), new("Q", ContentOperator.Restore),
        new("re", ContentOperator.Rectangle), new("RG", ContentOperator.SetStrokeRgb), new("rg", ContentOperator.SetFillRgb),
        new("ri", ContentOperator.SetRenderingIntent), new("s", ContentOperator.CloseStroke), new("S", ContentOperator.Stroke),
        new("SC", ContentOperator.SetStrokeColor), new("sc", ContentOperator.SetFillColor), new("SCN", ContentOperator.SetStrokeColorN),
        new("scn", ContentOperator.SetFillColorN), new("sh", ContentOperator.PaintShading), new("T*", ContentOperator.NextLine),
        new("Tc", ContentOperator.SetCharacterSpacing), new("Td", ContentOperator.MoveText), new("TD", ContentOperator.MoveTextSetLeading),
        new("Tf", ContentOperator.SetFont), new("Tj", ContentOperator.ShowText), new("TJ", ContentOperator.ShowTextArray),
        new("TL", ContentOperator.SetLeading), new("Tm", ContentOperator.SetTextMatrix), new("Tr", ContentOperator.SetRenderMode),
        new("Ts", ContentOperator.SetRise), new("Tw", ContentOperator.SetWordSpacing), new("Tz", ContentOperator.SetHorizontalScaling),
        new("v", ContentOperator.CurveToV), new("w", ContentOperator.SetLineWidth), new("W", ContentOperator.Clip), new("W*", ContentOperator.ClipEvenOdd),
        new("y", ContentOperator.CurveToY), new("'", ContentOperator.NextLineShowText), new("\"", ContentOperator.SetSpacingNextLineShowText),
    ];
}
