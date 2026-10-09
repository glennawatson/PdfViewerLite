// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;
using HyperPdfLibrary.Text;

namespace HyperPdfLibrary.PageObjects;

/// <content>Text state and text-showing operators.</content>
internal sealed partial class PageContentParser
{
    /// <summary>The string length decoded on the stack; longer strings use a pooled array.</summary>
    private const int StackStringBytes = 256;

    /// <summary>The percentage that is a horizontal scaling of 1.</summary>
    private const float PercentScale = 100;

    /// <summary>The thousandths of text space a TJ adjustment is given in.</summary>
    private const float AdjustmentUnits = 1000;

    /// <summary>The highest text render mode.</summary>
    private const int MaxRenderMode = 7;

    /// <summary>The operand index of the string in the <c>"</c> operator.</summary>
    private const int SpacingStringOperand = 2;

    /// <summary>The characters one code's text can take; matches <see cref="PdfFont.MaxUnicodeLength"/>.</summary>
    private const int UnicodeCapacity = 8;

    /// <summary>The glyphs of the show being read.</summary>
    private readonly List<PdfTextGlyph> _glyphs = [];

    /// <summary>The string bytes of the show being read.</summary>
    private readonly List<byte> _codeBytes = [];

    /// <summary>The Unicode text of the show being read.</summary>
    private readonly StringBuilder _text = new();

    /// <summary>The text matrix.</summary>
    private Matrix3x2 _tm = Matrix3x2.Identity;

    /// <summary>The text line matrix.</summary>
    private Matrix3x2 _tlm = Matrix3x2.Identity;

    /// <summary>How far the text matrix stands from the line matrix, along the text axes.</summary>
    private Vector2 _position;

    /// <summary>The <c>TJ</c> numbers read since the last glyph.</summary>
    private float _kerning;

    /// <summary>The total distance the show moved the text position along its writing direction.</summary>
    private float _showAdvance;

    /// <summary>Handles <c>BT</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpBeginText(PageContentParser self, ref ContentReader reader)
    {
        self._tm = Matrix3x2.Identity;
        self._tlm = Matrix3x2.Identity;
        self._position = Vector2.Zero;
    }

    /// <summary>Handles <c>Tc</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetCharacterSpacing(PageContentParser self, ref ContentReader reader) => self._state.CharacterSpacing = reader.Number(0);

    /// <summary>Handles <c>Tw</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetWordSpacing(PageContentParser self, ref ContentReader reader) => self._state.WordSpacing = reader.Number(0);

    /// <summary>Handles <c>Tz</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetHorizontalScaling(PageContentParser self, ref ContentReader reader) => self._state.HorizontalScaling = reader.Number(0) / PercentScale;

    /// <summary>Handles <c>TL</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetLeading(PageContentParser self, ref ContentReader reader) => self._state.Leading = reader.Number(0);

    /// <summary>Handles <c>Tf</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetFont(PageContentParser self, ref ContentReader reader)
    {
        var name = reader.Operand(0).Name;
        var value = self.FindResource(KnownName.Font, name);
        self._state.Font = value.AsDictionary() is { } dictionary ? self._cache.Fonts.Get(dictionary) : null;
        self._state.FontName = name;
        self._state.FontSize = reader.Number(1);
    }

    /// <summary>Handles <c>Tr</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetRenderMode(PageContentParser self, ref ContentReader reader) => self._state.RenderMode = Math.Clamp((int)reader.Number(0), 0, MaxRenderMode);

    /// <summary>Handles <c>Ts</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetRise(PageContentParser self, ref ContentReader reader) => self._state.Rise = reader.Number(0);

    /// <summary>Handles <c>Td</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpMoveText(PageContentParser self, ref ContentReader reader) => self.MoveText(reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>TD</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpMoveTextSetLeading(PageContentParser self, ref ContentReader reader)
    {
        self._state.Leading = -reader.Number(1);
        self.MoveText(reader.Number(0), reader.Number(1));
    }

    /// <summary>Handles <c>Tm</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetTextMatrix(PageContentParser self, ref ContentReader reader)
    {
        self._tlm = new(reader.Number(0), reader.Number(1), reader.Number(ThirdOperand), reader.Number(FourthOperand), reader.Number(FifthOperand), reader.Number(SixthOperand));
        self._tm = self._tlm;
        self._position = Vector2.Zero;
    }

    /// <summary>Handles <c>T*</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpNextLine(PageContentParser self, ref ContentReader reader) => self.MoveText(0, -self._state.Leading);

    /// <summary>Handles <c>Tj</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpShowText(PageContentParser self, ref ContentReader reader) => self.Show(ContentOperator.ShowText, ref reader, 0);

    /// <summary>Handles <c>TJ</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpShowTextArray(PageContentParser self, ref ContentReader reader) => self.Show(ContentOperator.ShowTextArray, ref reader, 0);

    /// <summary>Handles <c>'</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpNextLineShowText(PageContentParser self, ref ContentReader reader)
    {
        self.MoveText(0, -self._state.Leading);
        self.Show(ContentOperator.NextLineShowText, ref reader, 0);
    }

    /// <summary>Handles <c>"</c>.</summary>
    /// <param name="self">The parser.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSpacingNextLineShowText(PageContentParser self, ref ContentReader reader)
    {
        self._state.WordSpacing = reader.Number(0);
        self._state.CharacterSpacing = reader.Number(1);
        self.MoveText(0, -self._state.Leading);
        self.Show(ContentOperator.SetSpacingNextLineShowText, ref reader, SpacingStringOperand);
    }

    /// <summary>Gets the bounds of every glyph.</summary>
    /// <param name="glyphs">The glyphs.</param>
    /// <returns>The union of their boxes; empty when there are none.</returns>
    private static PdfRectangle UnionBoxes(PdfTextGlyph[] glyphs)
    {
        if (glyphs.Length == 0)
        {
            return default;
        }

        var bounds = glyphs[0].Box;
        for (var i = 1; i < glyphs.Length; i++)
        {
            bounds = bounds.Union(glyphs[i].Box);
        }

        return bounds;
    }

    /// <summary>Moves to the start of a new line.</summary>
    /// <param name="x">The x offset.</param>
    /// <param name="y">The y offset.</param>
    private void MoveText(float x, float y)
    {
        _tlm = Matrix3x2.CreateTranslation(x, y) * _tlm;
        _tm = _tlm;
        _position = Vector2.Zero;
    }

    /// <summary>Reads one text-showing operator into a text object.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="operandIndex">The operand holding the string or array.</param>
    private void Show(ContentOperator op, ref ContentReader reader, int operandIndex)
    {
        var operand = reader.Operand(operandIndex);
        var isArray = operand.Kind == ContentOperandKind.Array;
        if (!isArray && operand.Kind is not (ContentOperandKind.LiteralString or ContentOperandKind.HexString))
        {
            return;
        }

        _glyphs.Clear();
        _codeBytes.Clear();
        _ = _text.Clear();
        _kerning = 0;
        _showAdvance = 0;
        var startMatrix = _tm;
        if (isArray)
        {
            ShowArrayItems(reader.Body(operand));
        }
        else
        {
            ShowEncoded(operand.Kind == ContentOperandKind.HexString, reader.Body(operand));
        }

        AddTextObject(op, startMatrix, ref reader);
    }

    /// <summary>Makes the text object for the show that was read.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="startMatrix">The text matrix before the first glyph.</param>
    /// <param name="reader">The reader, for the <c>"</c> operator's spacing operands.</param>
    private void AddTextObject(ContentOperator op, Matrix3x2 startMatrix, ref ContentReader reader)
    {
        if (_glyphs.Count == 0 && _state.Font is not null)
        {
            // A show of only numbers or an empty string paints nothing; it only moves the text position.
            return;
        }

        var glyphs = _glyphs.ToArray();
        var item = new PdfTextObject
        {
            Font = _state.Font,
            FontName = _state.FontName,
            FontSize = _state.FontSize,
            RenderMode = _state.RenderMode,
            CharacterSpacing = _state.CharacterSpacing,
            WordSpacing = _state.WordSpacing,
            HorizontalScaling = _state.HorizontalScaling,
            Rise = _state.Rise,
            TextMatrix = startMatrix,
            Text = _text.ToString(),
            ShowOperator = op,
            SpacingOperandWord = op == ContentOperator.SetSpacingNextLineShowText ? reader.Number(0) : 0,
            SpacingOperandCharacter = op == ContentOperator.SetSpacingNextLineShowText ? reader.Number(1) : 0,
            CodeBytes = [.. _codeBytes],
            TrailingKerning = _kerning,
            TotalAdvance = _showAdvance,
            LineMatrixAfter = _tlm,
            PositionAfter = _position,
            IsOpaque = _state.Font is null,
        };
        item.SetGlyphs(glyphs);
        Add(item, UnionBoxes(glyphs), new(_operatorStart, _operatorEnd));
    }

    /// <summary>Shows the strings of a TJ array, moving by its numbers.</summary>
    /// <param name="body">The bytes between the brackets.</param>
    private void ShowArrayItems(ReadOnlySpan<byte> body)
    {
        var lexer = new PdfLexer(body);
        while (true)
        {
            var kind = lexer.Next();
            if (kind == PdfTokenKind.EndOfData)
            {
                return;
            }

            if (kind is PdfTokenKind.LiteralString or PdfTokenKind.HexString)
            {
                ShowEncoded(kind == PdfTokenKind.HexString, lexer.Lexeme);
            }
            else if (kind == PdfTokenKind.Number && PdfNumber.TryParseSingle(lexer.Lexeme, out var adjustment))
            {
                Adjust(adjustment);
            }
        }
    }

    /// <summary>Moves the text position by a TJ adjustment.</summary>
    /// <param name="adjustment">The adjustment in thousandths of text space.</param>
    private void Adjust(float adjustment)
    {
        _kerning += adjustment;
        var shift = -adjustment / AdjustmentUnits * _state.FontSize;
        var vertical = _state.Font?.IsVertical == true;
        var move = vertical ? shift : shift * _state.HorizontalScaling;
        Advance(vertical, move);
    }

    /// <summary>Moves the text position along the writing direction.</summary>
    /// <param name="vertical">Whether the text writes top to bottom.</param>
    /// <param name="move">The distance in text space units.</param>
    private void Advance(bool vertical, float move)
    {
        var step = vertical ? new Vector2(0, move) : new Vector2(move, 0);
        _tm = Matrix3x2.CreateTranslation(step) * _tm;
        _position += step;
        _showAdvance += move;
    }

    /// <summary>Decodes a string's escapes and shows it.</summary>
    /// <param name="hex">Whether the string is hexadecimal.</param>
    /// <param name="raw">The string's bytes between its delimiters.</param>
    private void ShowEncoded(bool hex, ReadOnlySpan<byte> raw)
    {
        byte[]? rented = null;
        Span<byte> buffer = raw.Length <= StackStringBytes ? stackalloc byte[StackStringBytes] : (rented = ArrayPool<byte>.Shared.Rent(raw.Length));
        try
        {
            var length = hex ? PdfStringDecoder.DecodeHex(raw, buffer) : PdfStringDecoder.DecodeLiteral(raw, buffer);
            ShowBytes(buffer[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<byte>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Shows the character codes in decoded string bytes.</summary>
    /// <param name="bytes">The bytes.</param>
    private void ShowBytes(ReadOnlySpan<byte> bytes)
    {
        var font = _state.Font;
        if (font is null)
        {
            return;
        }

        while (!bytes.IsEmpty)
        {
            var used = Math.Clamp(font.ReadCode(bytes, out var code), 1, bytes.Length);
            ShowCode(font, code, bytes[..used]);
            bytes = bytes[used..];
        }
    }

    /// <summary>Records one character code as a glyph and advances the text position.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="codeBytes">The bytes the code used.</param>
    private void ShowCode(PdfFont font, int code, ReadOnlySpan<byte> codeBytes)
    {
        var fontSize = _state.FontSize;
        var scaling = _state.HorizontalScaling;
        var vertical = font.IsVertical;
        var width = font.GetWidth(code);
        var advance = width;
        float originX = 0;
        float originY = 0;
        if (vertical)
        {
            font.GetVerticalMetrics(code, out advance, out originX, out originY);
        }

        var toUser = new Matrix3x2(fontSize * scaling, 0, 0, fontSize, 0, _state.Rise) * _tm * _state.Ctm;
        var shift = vertical ? Matrix3x2.CreateTranslation(-originX, -originY) : Matrix3x2.Identity;
        var box = TextGeometry.TransformRect(shift * toUser, new(0, font.Descent, width, font.Ascent));
        var origin = Vector2.Transform(Vector2.Transform(Vector2.Zero, shift), toUser);
        var spacing = _state.CharacterSpacing + (font.IsWordSpace(code, codeBytes.Length) ? _state.WordSpacing : 0);
        var move = vertical ? (advance * fontSize) + spacing : ((advance * fontSize) + spacing) * scaling;
        var textStart = _text.Length;
        AppendUnicode(font, code);
        var offset = _codeBytes.Count;
        foreach (var b in codeBytes)
        {
            _codeBytes.Add(b);
        }

        _glyphs.Add(new(code, offset, codeBytes.Length, textStart, _text.Length - textStart, _kerning, move, origin, box));
        _kerning = 0;
        Advance(vertical, move);
    }

    /// <summary>Appends a code's Unicode text.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    private void AppendUnicode(PdfFont font, int code)
    {
        Span<char> unicode = stackalloc char[UnicodeCapacity];
        var count = font.GetUnicode(code, unicode);
        _ = _text.Append(unicode[..Math.Clamp(count, 0, unicode.Length)]);
    }
}
