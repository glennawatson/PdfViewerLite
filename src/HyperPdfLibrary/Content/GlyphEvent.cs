// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Content;

/// <summary>One shown glyph, as the interpreter reports it to a device.</summary>
internal readonly ref struct GlyphEvent
{
    /// <summary>Initializes a new instance of the <see cref="GlyphEvent"/> struct.</summary>
    /// <param name="font">The font.</param>
    /// <param name="code">The character code.</param>
    /// <param name="unicode">The Unicode text of the code; empty when unknown.</param>
    /// <param name="placement">Where the glyph is drawn and how far the text position moves.</param>
    /// <param name="fontSize">The font size.</param>
    /// <param name="isWordSpace">Whether word spacing applied to the glyph.</param>
    internal GlyphEvent(PdfFont font, int code, ReadOnlySpan<char> unicode, in GlyphPlacement placement, float fontSize, bool isWordSpace)
    {
        Font = font;
        Code = code;
        Unicode = unicode;
        Placement = placement;
        FontSize = fontSize;
        IsWordSpace = isWordSpace;
    }

    /// <summary>Gets the font.</summary>
    internal PdfFont Font { get; }

    /// <summary>Gets the character code.</summary>
    internal int Code { get; }

    /// <summary>Gets the Unicode text of the code; empty when unknown.</summary>
    internal ReadOnlySpan<char> Unicode { get; }

    /// <summary>Gets where the glyph is drawn and how far the text position moves.</summary>
    internal GlyphPlacement Placement { get; }

    /// <summary>Gets the font size.</summary>
    internal float FontSize { get; }

    /// <summary>Gets a value indicating whether word spacing applied to the glyph.</summary>
    internal bool IsWordSpace { get; }

    /// <summary>Gets the matrix from glyph space to user space: font matrix, font size, scaling, rise and text matrix.</summary>
    internal Matrix3x2 GlyphMatrix => Placement.GlyphMatrix;

    /// <summary>Gets the text matrix at the glyph's origin, from text space to user space.</summary>
    internal Matrix3x2 TextMatrix => Placement.TextMatrix;

    /// <summary>Gets the distance the text position moved after the glyph, in text space units.</summary>
    internal float Advance => Placement.Advance;
}
