// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Annotations;

/// <summary>
/// Reads and changes the colours an appearance stream draws with, so a drawn annotation such as a stamp can be
/// recoloured without being laid out again.
/// </summary>
public static class PdfAppearanceColors
{
    /// <summary>The largest colour channel value.</summary>
    private const float ChannelMax = 255;

    /// <summary>The bit offset of red in 0xRRGGBB.</summary>
    private const int RedShift = 16;

    /// <summary>The bit offset of green in 0xRRGGBB.</summary>
    private const int GreenShift = 8;

    /// <summary>One channel's bits.</summary>
    private const uint ChannelMask = 0xFF;

    /// <summary>The index of the third operand.</summary>
    private const int Third = 2;

    /// <summary>The index of the fourth operand.</summary>
    private const int Fourth = 3;

    /// <summary>The longest colour operator, <c>scn</c>.</summary>
    private const int MaxColorOperator = 3;

    /// <summary>
    /// Reads the first colour an appearance sets, gray, RGB or CMYK, as readers that take an appearance's colour from
    /// its first object see it.
    /// </summary>
    /// <param name="appearance">The appearance stream.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns><see langword="true"/> when a colour was found.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static bool TryReadFirstColor(PdfStream appearance, PdfNameTable names, out uint color)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        ArgumentNullException.ThrowIfNull(names);
        var content = default(PooledBuffer);
        try
        {
            _ = appearance.Decode(ref content);
            return TryReadFirstColor(content.WrittenSpan, names, out color);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Rewrites an appearance so every colour it sets is one RGB colour; images keep their own colours.</summary>
    /// <param name="appearance">The appearance stream.</param>
    /// <param name="color">The colour as 0xRRGGBB.</param>
    /// <returns>The recoloured appearance, a new stream with the same entries, or <see langword="null"/> when it sets no colour or holds an inline image.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="appearance"/> is <see langword="null"/>.</exception>
    public static PdfStream? Recolor(PdfStream appearance, uint color)
    {
        ArgumentNullException.ThrowIfNull(appearance);
        var content = default(PooledBuffer);
        var builder = default(PdfContentBuilder);
        try
        {
            _ = appearance.Decode(ref content);
            if (!Rewrite(content.WrittenSpan, color, ref builder))
            {
                return null;
            }

            var dictionary = appearance.Dictionary;
            var bounds = dictionary.TryGetRectangle(KnownName.BBox, out var box) ? box : default;
            var form = builder.ToFormXObject(dictionary.Owner, bounds, dictionary.GetDictionary(KnownName.Resources));
            CopyEntries(dictionary, form.Dictionary);
            return form;
        }
        finally
        {
            builder.Dispose();
            content.Dispose();
        }
    }

    /// <summary>Reads the first colour operator of some content.</summary>
    /// <param name="content">The decoded content.</param>
    /// <param name="names">The name table.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when found.</returns>
    private static bool TryReadFirstColor(ReadOnlySpan<byte> content, PdfNameTable names, out uint color)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, names, operands);
        while (reader.Next(out var op))
        {
            if (TryGetColor(ref reader, op, out color))
            {
                return true;
            }
        }

        color = 0;
        return false;
    }

    /// <summary>Converts a gray, RGB or CMYK colour operator's operands.</summary>
    /// <param name="reader">The reader on the operator.</param>
    /// <param name="op">The operator.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> for a colour operator.</returns>
    private static bool TryGetColor(ref ContentReader reader, ContentOperator op, out uint color)
    {
        switch (op)
        {
            case ContentOperator.SetFillGray or ContentOperator.SetStrokeGray:
            {
                color = Pack(reader.Number(0), reader.Number(0), reader.Number(0));
                return true;
            }

            case ContentOperator.SetFillRgb or ContentOperator.SetStrokeRgb:
            {
                color = Pack(reader.Number(0), reader.Number(1), reader.Number(Third));
                return true;
            }

            case ContentOperator.SetFillCmyk or ContentOperator.SetStrokeCmyk:
            {
                var white = 1 - reader.Number(Fourth);
                color = Pack((1 - reader.Number(0)) * white, (1 - reader.Number(1)) * white, (1 - reader.Number(Third)) * white);
                return true;
            }

            default:
            {
                color = 0;
                return false;
            }
        }
    }

    /// <summary>Copies content, replacing each colour operator and its operands with the new colour.</summary>
    /// <param name="content">The content.</param>
    /// <param name="color">The colour.</param>
    /// <param name="builder">Receives the rewritten content.</param>
    /// <returns><see langword="true"/> when at least one colour was replaced and no inline image was met.</returns>
    private static bool Rewrite(ReadOnlySpan<byte> content, uint color, ref PdfContentBuilder builder)
    {
        var lexer = new PdfLexer(content);
        var copied = 0;
        var operandStart = -1;
        var changed = false;
        while (NextToken(ref lexer, out var start, out var kind))
        {
            if (kind != PdfTokenKind.Keyword || IsOperandKeyword(lexer.Lexeme))
            {
                operandStart = operandStart < 0 ? start : operandStart;
                continue;
            }

            if (lexer.Lexeme.SequenceEqual("BI"u8))
            {
                return false;
            }

            if (ReplaceColor(content[copied..(operandStart < 0 ? start : operandStart)], lexer.Lexeme, color, ref builder))
            {
                copied = lexer.Position;
                changed = true;
            }

            operandStart = -1;
        }

        builder.WriteRaw(content[copied..]);
        return changed;
    }

    /// <summary>Reads the next token, noting where it starts, including a string's or name's opening delimiter.</summary>
    /// <param name="lexer">The lexer.</param>
    /// <param name="start">Where the token starts.</param>
    /// <param name="kind">The token kind.</param>
    /// <returns><see langword="false"/> at the end of the content.</returns>
    private static bool NextToken(ref PdfLexer lexer, out int start, out PdfTokenKind kind)
    {
        lexer.SkipWhitespace();
        start = lexer.Position;
        kind = lexer.Next();
        return kind != PdfTokenKind.EndOfData;
    }

    /// <summary>Replaces a colour operator: copies the content before its operands, then writes the new colour.</summary>
    /// <param name="before">The content not yet copied that comes before the operator's operands.</param>
    /// <param name="keyword">The operator.</param>
    /// <param name="color">The colour.</param>
    /// <param name="builder">The rewritten content.</param>
    /// <returns><see langword="true"/> when the operator set a colour and was replaced.</returns>
    private static bool ReplaceColor(ReadOnlySpan<byte> before, ReadOnlySpan<byte> keyword, uint color, ref PdfContentBuilder builder)
    {
        if (ColorTarget(keyword) is not { } stroke)
        {
            return false;
        }

        builder.WriteRaw(before);
        WriteColor(ref builder, color, stroke);
        return true;
    }

    /// <summary>Writes an RGB colour operator, ending its line.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="color">The colour.</param>
    /// <param name="stroke">Whether it sets the stroke colour.</param>
    private static void WriteColor(ref PdfContentBuilder builder, uint color, bool stroke)
    {
        var red = ((color >> RedShift) & ChannelMask) / ChannelMax;
        var green = ((color >> GreenShift) & ChannelMask) / ChannelMax;
        var blue = (color & ChannelMask) / ChannelMax;
        if (stroke)
        {
            builder.SetStrokeRgb(red, green, blue);
        }
        else
        {
            builder.SetFillRgb(red, green, blue);
        }
    }

    /// <summary>Tells whether an operator sets a colour or colour space, and which.</summary>
    /// <param name="keyword">The operator.</param>
    /// <returns><see langword="true"/> for a stroke colour, <see langword="false"/> for a fill colour, <see langword="null"/> for other operators.</returns>
    private static bool? ColorTarget(ReadOnlySpan<byte> keyword)
    {
        if (keyword.IsEmpty || keyword.Length > MaxColorOperator)
        {
            return null;
        }

        // Fill operators are the lower case spellings of the stroke ones.
        Span<byte> lower = stackalloc byte[MaxColorOperator];
        _ = System.Text.Ascii.ToLower(keyword, lower, out var written);
        return IsColorOperator(lower[..written]) ? keyword[0] is >= (byte)'A' and <= (byte)'Z' : null;
    }

    /// <summary>Determines whether a lower case operator sets a colour or colour space.</summary>
    /// <param name="lower">The operator in lower case.</param>
    /// <returns><see langword="true"/> for <c>g</c>, <c>k</c>, <c>rg</c>, <c>cs</c>, <c>sc</c> and <c>scn</c>.</returns>
    private static bool IsColorOperator(ReadOnlySpan<byte> lower) =>
        lower.SequenceEqual("g"u8) || lower.SequenceEqual("k"u8) || lower.SequenceEqual("rg"u8)
        || lower.SequenceEqual("cs"u8) || lower.SequenceEqual("sc"u8) || lower.SequenceEqual("scn"u8);

    /// <summary>Determines whether a keyword is an operand: <c>true</c>, <c>false</c> or <c>null</c>.</summary>
    /// <param name="keyword">The keyword.</param>
    /// <returns><see langword="true"/> for an operand.</returns>
    private static bool IsOperandKeyword(ReadOnlySpan<byte> keyword) =>
        keyword.SequenceEqual("true"u8) || keyword.SequenceEqual("false"u8) || keyword.SequenceEqual("null"u8);

    /// <summary>Copies a form's entries other than those describing its data and box.</summary>
    /// <param name="source">The old form's dictionary.</param>
    /// <param name="target">The new form's dictionary.</param>
    private static void CopyEntries(PdfDictionary source, PdfDictionary target)
    {
        for (var i = 0; i < source.Count; i++)
        {
            var key = source.GetKeyAt(i);
            if (!target.ContainsKey(key) && !key.Is(KnownName.Filter) && !key.Is(KnownName.DecodeParms) && !key.Is(KnownName.Length))
            {
                target.Set(key, source.GetValueAt(i));
            }
        }
    }

    /// <summary>Packs three components from 0 to 1 into 0xRRGGBB.</summary>
    /// <param name="red">The red component.</param>
    /// <param name="green">The green component.</param>
    /// <param name="blue">The blue component.</param>
    /// <returns>The colour.</returns>
    private static uint Pack(float red, float green, float blue) =>
        (Channel(red) << RedShift) | (Channel(green) << GreenShift) | Channel(blue);

    /// <summary>Converts a component from 0 to 1 to a byte.</summary>
    /// <param name="component">The component.</param>
    /// <returns>The byte value.</returns>
    private static uint Channel(float component) => (uint)Math.Clamp(MathF.Round(component * ChannelMax), 0, ChannelMax);
}
