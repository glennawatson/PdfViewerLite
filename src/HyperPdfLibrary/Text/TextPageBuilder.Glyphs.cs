// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Numerics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <content>A run's glyphs: boxes, spaces from kerning and duplicate glyphs, as PDFium's ProcessTextObjectItems handles them.</content>
internal sealed partial class TextPageBuilder
{
    /// <summary>The share of the font size within which a repeated glyph is a duplicate.</summary>
    private const float DuplicateGlyphShare = 0.07F;

    /// <summary>The number of earlier characters checked for a duplicate glyph.</summary>
    private const int DuplicateGlyphLookBack = 7;

    /// <summary>The character spacing below which PDFium ignores it.</summary>
    private const float SpacingEpsilon = 0.001F;

    /// <summary>The share of the font size above which a space glyph is too wide to set the space threshold.</summary>
    private const float WideSpaceDivisor = 3;

    /// <summary>The divisor that turns a space glyph's width into the space threshold.</summary>
    private const float SpaceWidthDivisor = 2;

    /// <summary>The first width step of the space threshold inside a run.</summary>
    private const int KernStep1 = 300;

    /// <summary>The second width step of the space threshold inside a run.</summary>
    private const int KernStep2 = 500;

    /// <summary>The third width step of the space threshold inside a run.</summary>
    private const int KernStep3 = 700;

    /// <summary>The highest single-byte code searched for a font's space.</summary>
    private const int MaxSpaceCode = 255;

    /// <summary>The run length checked for direction on the stack.</summary>
    private const int StackDirectionChars = 256;

    /// <summary>The size of the buffer a code's Unicode text is written to, matching <see cref="PdfFont.MaxUnicodeLength"/>.</summary>
    private const int UnicodeBufferLength = 8;

    /// <summary>The glyph count of a run whose kerning PDFium never treats as base spacing.</summary>
    private const int KernedPairLength = 2;

    /// <summary>The vertical origin x PDFium measures loose vertical boxes from, in thousandths.</summary>
    private const float VerticalCentre = 500;

    /// <summary>Gets the correction for character spacing, as PDFium's CalculateBaseSpaceAdjustment does.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The adjustment.</returns>
    private static float BaseSpaceAdjustment(in TextRun run)
    {
        var characterSpacing = run.CharSpacing;
        if (characterSpacing > SpacingEpsilon)
        {
            return -TextGeometry.TransformDistance(run.Matrix, characterSpacing);
        }

        return characterSpacing < -SpacingEpsilon ? TextGeometry.TransformDistance(run.Matrix, MathF.Abs(characterSpacing)) : 0;
    }

    /// <summary>Determines whether a code's text is a single space.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The code.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsSpace(PdfFont font, int code)
    {
        Span<char> text = stackalloc char[UnicodeBufferLength];
        return font.GetUnicode(code, text) == 1 && text[0] == ' ';
    }

    /// <summary>Gets a glyph's outline box in user space, with PDFium's fallbacks for blank glyphs.</summary>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph.</param>
    /// <returns>The box.</returns>
    private static PdfRectangle CharBox(in TextRun run, in TextGlyph glyph)
    {
        var scale = run.FontSize / GlyphUnits;
        var origin = ItemOrigin(run, glyph);
        var box = glyph.Box;
        var left = (box.Left * scale) + origin.X;
        var bottom = (box.Bottom * scale) + origin.Y;
        var right = (box.Right * scale) + origin.X;
        var top = (box.Top * scale) + origin.Y;
        if (MathF.Abs(top - bottom) < SizeEpsilon)
        {
            top = bottom + scale;
        }

        if (MathF.Abs(right - left) < SizeEpsilon)
        {
            right = left + glyph.CharWidth;
        }

        return TextGeometry.TransformRect(run.Matrix, new(left, bottom, right, top));
    }

    /// <summary>Gets a glyph's box from its advance and the font's ascent and descent, as PDFium's GetLooseBounds does.</summary>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="origin">The glyph origin in user space.</param>
    /// <param name="box">The outline box in user space.</param>
    /// <returns>The loose box, joined with the outline box.</returns>
    private static PdfRectangle LooseBox(in TextRun run, in TextGlyph glyph, Vector2 origin, in PdfRectangle box)
    {
        var fontSize = run.FontSize;
        if (TextGeometry.IsEmpty(box) || fontSize == 0)
        {
            return box;
        }

        var font = run.Font;
        if (font.IsVertical)
        {
            var left = origin.X + ((glyph.VerticalOrigin.X - VerticalCentre) * fontSize / GlyphUnits);
            var top = origin.Y + (glyph.VerticalOrigin.Y * fontSize / GlyphUnits);
            return TextGeometry.Union(new(left, top + glyph.CharWidth, left + fontSize, top), box);
        }

        var ascent = font.Ascent * GlyphUnits;
        var descent = font.Descent * GlyphUnits;
        if (TextGeometry.Same(ascent, descent))
        {
            return box;
        }

        var local = ItemOrigin(run, glyph);
        var loose = new PdfRectangle(local.X, local.Y + (descent * fontSize / GlyphUnits), local.X + glyph.CharWidth, local.Y + (ascent * fontSize / GlyphUnits));
        return TextGeometry.Union(TextGeometry.TransformRect(run.Matrix, loose), box);
    }

    /// <summary>Adds a run's glyphs to the line being built.</summary>
    /// <param name="index">The run.</param>
    /// <returns><see langword="true"/> when the run is right-to-left text drawn mirrored, so its characters must be reversed.</returns>
    private bool AddRunGlyphs(int index)
    {
        var run = _runs[index];
        var baseSpace = BaseSpace(run) + BaseSpaceAdjustment(run);
        var glyphs = GlyphsOf(run);
        for (var i = 0; i < glyphs.Length; i++)
        {
            var spacing = (i > 0 ? KerningSpacing(run, glyphs[i - 1]) : 0) - baseSpace;
            if (i > 0 && spacing != 0 && IsKerningSpace(run, glyphs[i], spacing))
            {
                AddKerningSpace(run, index, glyphs[i]);
            }

            AddGlyph(run, index, glyphs[i], i);
        }

        var matrix = run.Matrix;
        return IsRightToLeft(run) && (matrix.M11 * matrix.M22) - (matrix.M12 * matrix.M21) < 0;
    }

    /// <summary>Determines whether a gap inside a run is wide enough to be a space.</summary>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph after the gap.</param>
    /// <param name="spacing">The gap.</param>
    /// <returns><see langword="true"/> when it is a space.</returns>
    private bool IsKerningSpace(in TextRun run, in TextGlyph glyph, float spacing)
    {
        var threshold = SpaceThreshold(run, glyph);
        return threshold != 0 && spacing >= threshold;
    }

    /// <summary>Gets the space a TJ adjustment after a glyph opens, in user space.</summary>
    /// <param name="run">The run.</param>
    /// <param name="previous">The glyph before the gap.</param>
    /// <returns>The spacing; 0 when there is no adjustment or the text already ends with a space.</returns>
    private float KerningSpacing(in TextRun run, in TextGlyph previous)
    {
        var text = _tempText.Count > 0 ? _tempText : _text;
        var opens = previous.Kerning != 0 && text.Count > 0 && text[^1] != ' ';
        return opens ? -run.FontSizeH * previous.Kerning / GlyphUnits : 0;
    }

    /// <summary>Gets the character spacing PDFium treats as the normal gap, as its CalculateBaseSpace does.</summary>
    /// <param name="run">The run.</param>
    /// <returns>The base space.</returns>
    private float BaseSpace(in TextRun run)
    {
        var characterSpacing = run.CharSpacing;
        if (characterSpacing == 0 || run.GlyphCount < KernedPairLength)
        {
            return 0;
        }

        var spacing = TextGeometry.TransformDistance(run.Matrix, characterSpacing);
        var fontSize = run.FontSizeH;
        var baseSpace = spacing;
        var kerned = false;
        foreach (var glyph in GlyphsOf(run))
        {
            if (glyph.Kerning == 0)
            {
                continue;
            }

            baseSpace = MathF.Min(baseSpace, (-fontSize * glyph.Kerning / GlyphUnits) + spacing);
            kerned = true;
        }

        return baseSpace < 0 || (run.GlyphCount == KernedPairLength && kerned) ? 0 : baseSpace;
    }

    /// <summary>Gets the gap inside a run that makes a space, as PDFium's CalculateSpaceThreshold does.</summary>
    /// <param name="run">The run.</param>
    /// <param name="glyph">The glyph after the gap.</param>
    /// <returns>The threshold in user space.</returns>
    private float SpaceThreshold(in TextRun run, in TextGlyph glyph)
    {
        var fontSize = run.FontSizeH;
        var spaceCode = SpaceCodeOf(run.Font);
        var threshold = spaceCode >= 0 ? fontSize * MathF.Round(run.Font.GetWidth(spaceCode) * GlyphUnits) / GlyphUnits : 0;
        threshold = threshold > fontSize / WideSpaceDivisor ? 0 : threshold / SpaceWidthDivisor;
        if (threshold == 0)
        {
            threshold = NormalizeThreshold(glyph.WidthUnits, KernStep1, KernStep2, KernStep3);
            threshold = fontSize * threshold / GlyphUnits;
        }

        return threshold;
    }

    /// <summary>Finds the code a font shows a space with, as PDFium's CharCodeFromUnicode does for a space.</summary>
    /// <param name="font">The font.</param>
    /// <returns>The code, or -1 when no single-byte code maps to a space.</returns>
    private int SpaceCodeOf(PdfFont font)
    {
        if (_spaceCodes.TryGetValue(font, out var known))
        {
            return known;
        }

        var found = IsSpace(font, ' ') ? ' ' : -1;
        for (var code = 0; found < 0 && code <= MaxSpaceCode; code++)
        {
            found = IsSpace(font, code) ? code : -1;
        }

        _spaceCodes[font] = found;
        return found;
    }

    /// <summary>Adds a space opened by kerning before a glyph.</summary>
    /// <param name="run">The run.</param>
    /// <param name="index">The run index.</param>
    /// <param name="glyph">The glyph after the space.</param>
    private void AddKerningSpace(in TextRun run, int index, in TextGlyph glyph)
    {
        var origin = TextGeometry.Transform(run.Matrix, ItemOrigin(run, glyph));
        var box = new PdfRectangle(origin.X, origin.Y, origin.X, origin.Y);
        _tempText.Add(' ');
        _temp.Add(new(PdfTextCharKind.Generated, -1, ' ', origin, box, box, Matrix3x2.Identity, index, 0));
    }

    /// <summary>Adds one glyph's characters, dropping it when it repeats a glyph just shown at the same place.</summary>
    /// <param name="run">The run.</param>
    /// <param name="index">The run index.</param>
    /// <param name="glyph">The glyph.</param>
    /// <param name="position">The glyph's index in the run.</param>
    private void AddGlyph(in TextRun run, int index, in TextGlyph glyph, int position)
    {
        scoped var text = TextOf(glyph);
        var kind = PdfTextCharKind.Normal;
        Span<char> code = [(char)glyph.Code];
        if (text.IsEmpty && glyph.Code != 0)
        {
            text = code;
            kind = PdfTextCharKind.NotUnicode;
        }

        var origin = TextGeometry.Transform(run.Matrix, ItemOrigin(run, glyph));
        var box = CharBox(run, glyph);
        var info = new TextBuildChar(kind, glyph.Code, '\0', origin, box, LooseBox(run, glyph, origin, box), run.Matrix, index, glyph.WidthUnits);
        if (text.IsEmpty)
        {
            _temp.Add(info);
            _tempText.Add(NoText);
            return;
        }

        if (IsDuplicateGlyph(run, info))
        {
            DropSpaceBeforeDuplicate(position);
            return;
        }

        foreach (var value in text)
        {
            _temp.Add(info with { Unicode = value });
            _tempText.Add(value == '\0' ? NoText : value);
        }
    }

    /// <summary>Drops a generated space before a run whose first glyph is a duplicate.</summary>
    /// <param name="position">The glyph's index in its run.</param>
    private void DropSpaceBeforeDuplicate(int position)
    {
        if (position != 0 || _tempText.Count == 0 || _tempText[^1] != ' ')
        {
            return;
        }

        _tempText.RemoveAt(_tempText.Count - 1);
        _temp.RemoveAt(_temp.Count - 1);
    }

    /// <summary>Determines whether a glyph repeats one of the last few glyphs of the same font at nearly the same place.</summary>
    /// <param name="run">The run.</param>
    /// <param name="info">The glyph's character.</param>
    /// <returns><see langword="true"/> when it is a duplicate.</returns>
    private bool IsDuplicateGlyph(in TextRun run, in TextBuildChar info)
    {
        var count = Math.Min(_temp.Count, DuplicateGlyphLookBack);
        var threshold = TextGeometry.TransformXDistance(info.Matrix, DuplicateGlyphShare * run.FontSize);
        for (var n = _temp.Count; n > _temp.Count - count; n--)
        {
            var candidate = _temp[n - 1];
            if (candidate.Code != info.Code || candidate.Run < 0 || !ReferenceEquals(_runs[candidate.Run].Font, run.Font))
            {
                continue;
            }

            var diff = candidate.Origin - info.Origin;
            if (MathF.Abs(diff.X) < threshold && MathF.Abs(diff.Y) < threshold)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a run reads right to left, from the first character of each glyph, as PDFium's IsRightToLeft does.</summary>
    /// <param name="run">The run.</param>
    /// <returns><see langword="true"/> for right-to-left text.</returns>
    private bool IsRightToLeft(in TextRun run)
    {
        var glyphs = GlyphsOf(run);
        char[]? rented = null;
        var buffer = glyphs.Length <= StackDirectionChars ? stackalloc char[StackDirectionChars] : (rented = ArrayPool<char>.Shared.Rent(glyphs.Length));
        try
        {
            var length = 0;
            foreach (var glyph in glyphs)
            {
                var value = FirstCharOf(glyph);
                buffer[length] = value;
                length += value == '\0' ? 0 : 1;
            }

            return TextUnicode.IsRightToLeft(buffer[..length]);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }
}
