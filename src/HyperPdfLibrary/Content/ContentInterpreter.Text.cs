// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Syntax;
using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <content>Text objects, text state and showing text.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The string length decoded on the stack; longer strings use a pooled array.</summary>
    private const int StackStringBytes = 256;

    /// <summary>The percentage that is a horizontal scaling of 1.</summary>
    private const float PercentScale = 100;

    /// <summary>The thousandths of text space a TJ adjustment is given in.</summary>
    private const float AdjustmentUnits = 1000;

    /// <summary>The highest text render mode.</summary>
    private const int MaxRenderMode = 7;

    /// <summary>The first text render mode that adds to the clip.</summary>
    private const int FirstClipMode = 4;

    /// <summary>The invisible text render mode.</summary>
    private const int InvisibleMode = 3;

    /// <summary>The operand index of the string in the <c>"</c> operator.</summary>
    private const int SpacingStringOperand = 2;

    /// <summary>Handles <c>BT</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpBeginText(ContentInterpreter self, ref ContentReader reader)
    {
        self._tm = Matrix3x2.Identity;
        self._tlm = Matrix3x2.Identity;
    }

    /// <summary>Handles <c>ET</c>: applies the clip collected by the clipping render modes.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpEndText(ContentInterpreter self, ref ContentReader reader)
    {
        if (self._textClipGlyphs <= 0)
        {
            return;
        }

        using var clip = self._textClip.Detach();
        self._textClipGlyphs = 0;
        self._device.Clip(clip, false, self._state.Ctm);
    }

    /// <summary>Handles <c>Tc</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetCharacterSpacing(ContentInterpreter self, ref ContentReader reader) => self._state.CharacterSpacing = reader.Number(0);

    /// <summary>Handles <c>Tw</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetWordSpacing(ContentInterpreter self, ref ContentReader reader) => self._state.WordSpacing = reader.Number(0);

    /// <summary>Handles <c>Tz</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetHorizontalScaling(ContentInterpreter self, ref ContentReader reader) => self._state.HorizontalScaling = reader.Number(0) / PercentScale;

    /// <summary>Handles <c>TL</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetLeading(ContentInterpreter self, ref ContentReader reader) => self._state.Leading = reader.Number(0);

    /// <summary>Handles <c>Tf</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetFont(ContentInterpreter self, ref ContentReader reader)
    {
        var value = self.FindResource(KnownName.Font, reader.Operand(0).Name);
        self._state.Font = value.AsDictionary() is { } dictionary ? self._cache.Fonts.Get(dictionary) : null;
        self._state.FontSize = reader.Number(1);
    }

    /// <summary>Handles <c>Tr</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetRenderMode(ContentInterpreter self, ref ContentReader reader) => self._state.RenderMode = Math.Clamp((int)reader.Number(0), 0, MaxRenderMode);

    /// <summary>Handles <c>Ts</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetRise(ContentInterpreter self, ref ContentReader reader) => self._state.Rise = reader.Number(0);

    /// <summary>Handles <c>Td</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpMoveText(ContentInterpreter self, ref ContentReader reader) => self.MoveText(reader.Number(0), reader.Number(1));

    /// <summary>Handles <c>TD</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpMoveTextSetLeading(ContentInterpreter self, ref ContentReader reader)
    {
        self._state.Leading = -reader.Number(1);
        self.MoveText(reader.Number(0), reader.Number(1));
    }

    /// <summary>Handles <c>Tm</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetTextMatrix(ContentInterpreter self, ref ContentReader reader)
    {
        self._tlm = new(reader.Number(0), reader.Number(1), reader.Number(MatrixC), reader.Number(MatrixD), reader.Number(MatrixE), reader.Number(MatrixF));
        self._tm = self._tlm;
    }

    /// <summary>Handles <c>T*</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpNextLine(ContentInterpreter self, ref ContentReader reader) => self.MoveText(0, -self._state.Leading);

    /// <summary>Handles <c>Tj</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OpShowText(ContentInterpreter self, ref ContentReader reader) => self.ShowOperand(ref reader, 0);

    /// <summary>Handles <c>TJ</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpShowTextArray(ContentInterpreter self, ref ContentReader reader)
    {
        var array = reader.Operand(0);
        if (array.Kind != ContentOperandKind.Array)
        {
            return;
        }

        self._textObjects?.BeginTextObject();
        self.ShowArray(reader.Body(array));
    }

    /// <summary>Handles <c>'</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpNextLineShowText(ContentInterpreter self, ref ContentReader reader)
    {
        self.MoveText(0, -self._state.Leading);
        self.ShowOperand(ref reader, 0);
    }

    /// <summary>Handles <c>"</c>.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSpacingNextLineShowText(ContentInterpreter self, ref ContentReader reader)
    {
        self._state.WordSpacing = reader.Number(0);
        self._state.CharacterSpacing = reader.Number(1);
        self.MoveText(0, -self._state.Leading);
        self.ShowOperand(ref reader, SpacingStringOperand);
    }

    /// <summary>Handles <c>d1</c>: the glyph is a shape painted in the colour in force when it is shown.</summary>
    /// <param name="self">The interpreter.</param>
    /// <param name="reader">The reader.</param>
    private static void OpSetGlyphWidthAndBounds(ContentInterpreter self, ref ContentReader reader)
    {
        self._state.Stroke = self._state.Fill;
        self._colorLocked = true;
    }

    /// <summary>Moves to the start of a new line.</summary>
    /// <param name="x">The x offset.</param>
    /// <param name="y">The y offset.</param>
    private void MoveText(float x, float y)
    {
        _tlm = Matrix3x2.CreateTranslation(x, y) * _tlm;
        _tm = _tlm;
    }

    /// <summary>Shows the string operand at an index.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="index">The operand index.</param>
    private void ShowOperand(ref ContentReader reader, int index)
    {
        var operand = reader.Operand(index);
        if (operand.Kind is not (ContentOperandKind.LiteralString or ContentOperandKind.HexString))
        {
            return;
        }

        _textObjects?.BeginTextObject();
        var group = BeginTextKnockout();
        try
        {
            ShowEncoded(operand.Kind == ContentOperandKind.HexString, reader.Body(operand));
        }
        finally
        {
            EndTextKnockout(group);
        }
    }

    /// <summary>Shows the strings of a TJ array, moving by its numbers.</summary>
    /// <param name="body">The bytes between the brackets.</param>
    private void ShowArray(ReadOnlySpan<byte> body)
    {
        var group = BeginTextKnockout();
        try
        {
            ShowArrayItems(body);
        }
        finally
        {
            EndTextKnockout(group);
        }
    }

    /// <summary>
    /// Starts a knockout group around one text show when text knockout (/TK) is on and the text is translucent or
    /// blended, so overlapping glyphs of the show do not show through each other; the group carries the alpha, blend
    /// mode and soft mask, and the glyphs inside are drawn opaque.
    /// </summary>
    /// <returns>The group to end, or null when the glyphs are drawn directly.</returns>
    private GroupInfo? BeginTextKnockout()
    {
        if (!UsesTextKnockout())
        {
            return null;
        }

        var group = new GroupInfo(true, true, _state.FillAlpha, _state.BlendMode, _state.SoftMask, SKRect.Empty);
        _device.BeginGroup(group);
        _state.FillAlpha = 1;
        _state.BlendMode = PdfBlendMode.Normal;
        _state.SoftMask = null;
        return group;
    }

    /// <summary>Ends a text knockout group and restores the state it carried.</summary>
    /// <param name="group">The group from <see cref="BeginTextKnockout"/>, or null.</param>
    private void EndTextKnockout(GroupInfo? group)
    {
        if (group is not { } info)
        {
            return;
        }

        _device.EndGroup(info);
        _state.FillAlpha = info.Alpha;
        _state.BlendMode = info.Blend;
        _state.SoftMask = info.SoftMask;
    }

    /// <summary>Determines whether a text show needs a knockout group: visible filled text, not Type 3, with /TK and transparency.</summary>
    /// <returns><see langword="true"/> when the show is wrapped in a group.</returns>
    private bool UsesTextKnockout() =>
        _hidden == 0
        && _state.TextKnockout
        && _state.RenderMode is 0 or FirstClipMode
        && _state.Font is not null and not PdfType3Font
        && HasTransparency();

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
        var shift = -adjustment / AdjustmentUnits * _state.FontSize;
        var vertical = _state.Font?.IsVertical == true;
        _tm = (vertical ? Matrix3x2.CreateTranslation(0, shift) : Matrix3x2.CreateTranslation(shift * _state.HorizontalScaling, 0)) * _tm;
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
            ShowCode(font, code, used);
            bytes = bytes[used..];
        }
    }

    /// <summary>Shows one character code and advances the text position.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="length">The bytes the code used.</param>
    private void ShowCode(PdfFont font, int code, int length)
    {
        var fontSize = _state.FontSize;
        var scaling = _state.HorizontalScaling;
        var vertical = font.IsVertical;
        var advance = font.GetWidth(code);
        float originX = 0;
        float originY = 0;
        if (vertical)
        {
            font.GetVerticalMetrics(code, out advance, out originX, out originY);
        }

        var origin = vertical ? Matrix3x2.CreateTranslation(-originX, -originY) : Matrix3x2.Identity;
        var glyphMatrix = font.FontMatrix * origin * new Matrix3x2(fontSize * scaling, 0, 0, fontSize, 0, _state.Rise) * _tm;
        var isSpace = font.IsWordSpace(code, length);
        var spacing = _state.CharacterSpacing + (isSpace ? _state.WordSpacing : 0);
        var move = vertical ? (advance * fontSize) + spacing : ((advance * fontSize) + spacing) * scaling;
        if (_hidden == 0)
        {
            Span<char> unicode = stackalloc char[PdfFont.MaxUnicodeLength];
            var count = font.GetUnicode(code, unicode);
            var glyph = new GlyphEvent(font, code, unicode[..Math.Clamp(count, 0, unicode.Length)], new(glyphMatrix, _tm, move), fontSize, isSpace);
            _device.DrawGlyph(glyph, ref _state);
            DrawGlyphContent(font, code, glyphMatrix);
        }
        else
        {
            // Hidden optional content draws nothing, but its clipping text still clips what follows.
            AddTextClip(font, code, glyphMatrix);
        }

        _tm = (vertical ? Matrix3x2.CreateTranslation(0, move) : Matrix3x2.CreateTranslation(move, 0)) * _tm;
    }

    /// <summary>Runs a Type 3 glyph's content, or adds a glyph outline to the text clip.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    private void DrawGlyphContent(PdfFont font, int code, Matrix3x2 glyphMatrix)
    {
        if (font is PdfType3Font type3)
        {
            if (_state.RenderMode != InvisibleMode)
            {
                DrawType3Glyph(type3, code, glyphMatrix);
            }

            return;
        }

        AddTextClip(font, code, glyphMatrix);
    }

    /// <summary>Adds a glyph's outline to the text clip when the render mode clips (4 to 7).</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    private void AddTextClip(PdfFont font, int code, Matrix3x2 glyphMatrix)
    {
        if (_state.RenderMode < FirstClipMode || font is PdfType3Font || font.GetOutline(code) is not { } outline)
        {
            return;
        }

        var transform = SkiaConversions.ToSkMatrix(glyphMatrix);
        _textClip.AddPath(outline, in transform);
        _textClipGlyphs++;
    }

    /// <summary>Runs a Type 3 glyph's content stream.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="glyphMatrix">The matrix from glyph space to user space.</param>
    private void DrawType3Glyph(PdfType3Font font, int code, Matrix3x2 glyphMatrix)
    {
        if (_depth >= PdfLimits.MaxDrawDepth || font.GetCharProc(code) is not { } procedure)
        {
            return;
        }

        var tm = _tm;
        var tlm = _tlm;
        var locked = _colorLocked;
        var patternBase = _patternBase;
        SaveState();
        _state.Ctm = glyphMatrix * _state.Ctm;
        _state.Font = null;
        _resources.Add(font.Resources ?? CurrentResources());
        _depth++;
        try
        {
            RunStreamBody(procedure);
        }
        finally
        {
            _depth--;
            _resources.RemoveAt(_resources.Count - 1);
            RestoreState();
            _tm = tm;
            _tlm = tlm;
            _colorLocked = locked;
            _patternBase = patternBase;
        }
    }
}
