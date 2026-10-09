// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.Programs;
using SkiaSharp;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A system font standing in for a font the PDF does not embed. Faces are shared by every document and live for the
/// process, so the cache never disposes them. Skia calls are serialised by a lock; they run only while a glyph is first
/// loaded.
/// </summary>
[DebuggerDisplay("SubstituteFace: {FamilyName}")]
internal sealed class SubstituteFace : GlyphSource, IDisposable
{
    /// <summary>The glyph space units per em, used as the Skia font size so paths come out in glyph space.</summary>
    private const float GlyphUnits = 1000F;

    /// <summary>The page of private-use code points Microsoft symbol fonts map their codes into.</summary>
    private const int SymbolPage = 0xF000;

    /// <summary>The largest single-byte code.</summary>
    private const int MaxByteCode = 0xFF;

    /// <summary>The horizontal skew of a synthetic italic, about 11 degrees; negative leans right in Skia's y-down space.</summary>
    private const float SyntheticSkew = -0.2F;

    /// <summary>Guards the Skia font.</summary>
    private readonly Lock _gate = new();

    /// <summary>The Skia font at 1000 units per em, unhinted; null for a bundled face.</summary>
    private readonly SKFont? _font;

    /// <summary>The managed glyph source of a bundled face; null for a system face.</summary>
    private readonly ProgramGlyphSource? _bundled;

    /// <summary>Initializes a new instance of the <see cref="SubstituteFace"/> class from a system font.</summary>
    /// <param name="typeface">The typeface; the face owns it.</param>
    /// <param name="isRequestedFamily">Whether the typeface is the family the PDF asked for rather than a stand-in.</param>
    /// <param name="style">The synthetic bold and slant to add when the typeface lacks them.</param>
    internal SubstituteFace(SKTypeface typeface, bool isRequestedFamily, SyntheticStyle style)
    {
        Typeface = typeface;
        IsRequestedFamily = isRequestedFamily;
        FamilyName = typeface.FamilyName ?? string.Empty;
        _font = new(typeface, GlyphUnits) { Hinting = SKFontHinting.None, LinearMetrics = true, Subpixel = true, Embolden = style.Bold, SkewX = style.Slant ? SyntheticSkew : 0 };
        _ = _font.GetFontMetrics(out var metrics);
        Ascent = -metrics.Ascent;
        Descent = -metrics.Descent;
        GlyphCount = typeface.GlyphCount;
    }

    /// <summary>Initializes a new instance of the <see cref="SubstituteFace"/> class from a bundled font program.</summary>
    /// <param name="program">The bundled program, decoded by the managed parser.</param>
    /// <param name="familyName">The face's name.</param>
    internal SubstituteFace(FontProgram program, string familyName)
    {
        _bundled = new(program);
        FamilyName = familyName;
        Ascent = _bundled.Ascent;
        Descent = _bundled.Descent;
        GlyphCount = program.GlyphCount;
    }

    /// <summary>Gets the system typeface, or <see langword="null"/> for a bundled face.</summary>
    internal SKTypeface? Typeface { get; }

    /// <summary>Gets a value indicating whether the face is one of the fonts bundled with the library.</summary>
    internal bool IsBundled => _bundled is not null;

    /// <summary>Gets the family name of the typeface.</summary>
    internal string FamilyName { get; }

    /// <summary>Gets a value indicating whether the typeface is the family the PDF named, not a generic stand-in.</summary>
    internal bool IsRequestedFamily { get; }

    /// <inheritdoc/>
    internal override int GlyphCount { get; }

    /// <inheritdoc/>
    internal override float Ascent { get; }

    /// <inheritdoc/>
    internal override float Descent { get; }

    /// <inheritdoc/>
    internal override bool IsSubstitute => true;

    /// <inheritdoc/>
    public void Dispose()
    {
        _font?.Dispose();
        Typeface?.Dispose();
    }

    /// <summary>Finds the glyph of a Unicode code point.</summary>
    /// <param name="codePoint">The code point.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetGlyph(int codePoint)
    {
        if (codePoint <= 0)
        {
            return 0;
        }

        if (_bundled is not null)
        {
            return Math.Max(_bundled.Program.GetGlyphByUnicode(codePoint), 0);
        }

        lock (_gate)
        {
            return _font!.GetGlyph(codePoint);
        }
    }

    /// <summary>Finds a glyph by its PostScript name; only a bundled face keeps names.</summary>
    /// <param name="name">The glyph name's UTF-8 bytes.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetNamedGlyph(ReadOnlySpan<byte> name) => _bundled is null ? 0 : Math.Max(_bundled.Program.GetGlyphByName(name), 0);

    /// <summary>Finds the glyph of a symbol font code: the code itself, then the code in the F000 page.</summary>
    /// <param name="code">The single-byte code.</param>
    /// <returns>The glyph id, or zero when the face has none.</returns>
    internal int GetSymbolGlyph(int code)
    {
        if ((uint)code > MaxByteCode)
        {
            return 0;
        }

        if (_bundled is not null)
        {
            return Math.Max(_bundled.Program.GetGlyphByCharCode(code), 0);
        }

        var glyph = GetGlyph(code);
        return glyph != 0 ? glyph : GetGlyph(SymbolPage | code);
    }

    /// <inheritdoc/>
    internal override SKPath? BuildOutline(int glyph)
    {
        if (_bundled is not null)
        {
            return _bundled.BuildOutline(glyph);
        }

        if ((uint)glyph >= (uint)GlyphCount)
        {
            return null;
        }

        SKPath? path;
        lock (_gate)
        {
            path = _font!.GetGlyphPath((ushort)glyph);
        }

        // Skia paths have y pointing down; glyph space has it pointing up.
        path?.Transform(SKMatrix.CreateScale(1, -1));
        return path;
    }

    /// <inheritdoc/>
    internal override float GetAdvance(int glyph)
    {
        if ((uint)glyph >= (uint)GlyphCount)
        {
            return 0;
        }

        if (_bundled is not null)
        {
            return _bundled.GetAdvance(glyph);
        }

        ReadOnlySpan<ushort> glyphs = [(ushort)glyph];
        Span<float> widths = stackalloc float[1];
        lock (_gate)
        {
            _font!.GetGlyphWidths(glyphs, widths, []);
        }

        return widths[0];
    }
}
