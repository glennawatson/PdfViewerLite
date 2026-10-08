// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;
using PdfViewerLite.Pdfium.Native;
using PdfViewerLite.Pdfium.Text;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// Choosing and embedding the fonts formatted text is written in. The chosen installed family is embedded when its
/// licence allows and it has every character; otherwise the closest built in family is used when it has every
/// character, and otherwise the first installed font that has them all. Callers hold the PDFium lock.
/// </summary>
internal sealed partial class PdfiumFonts
{
    /// <summary>The characters a base font name takes, with its terminator.</summary>
    private const int BaseFontChars = 32;

    /// <summary>The built in fonts loaded so far, by base font name.</summary>
    private readonly Dictionary<string, StandardFontShaper> _standard = [with(StringComparer.Ordinal)];

    /// <summary>The installed fonts embedded so far.</summary>
    private readonly Dictionary<FontProgram, EmbeddedFont> _embedded = [];

    /// <summary>The installed fonts, or <see langword="null"/> to use the system's.</summary>
    private FontCatalog? _catalog;

    /// <summary>Gets or sets the installed fonts text may be written in; the system's fonts unless a test sets others.</summary>
    internal FontCatalog Catalog
    {
        get => _catalog ?? FontCatalog.System;
        set => _catalog = value;
    }

    /// <summary>Gets the layout reused for every text box, so writing text allocates no layout buffers.</summary>
    internal TextBoxLayout Layout { get; } = new();

    /// <summary>Determines whether a font has every character of some text, white space aside.</summary>
    /// <param name="program">The font.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has them all.</returns>
    internal static bool Covers(FontProgram program, string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (!Rune.IsWhiteSpace(rune) && !Rune.IsControl(rune) && program.GlyphFor(rune.Value) == 0)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Chooses the font to write text in.</summary>
    /// <param name="format">The format.</param>
    /// <param name="text">The text.</param>
    /// <returns>The font, or <see langword="null"/> when no font can be loaded.</returns>
    internal TextFont? Resolve(TextFormat format, string text)
    {
        var match = FindInstalled(format, out var program);
        if (program is not null && Covers(program, text))
        {
            return Installed(program, match!.SynthesizeBold, match.SynthesizeItalic);
        }

        var standard = Standard(ClosestStandard(format.FontFamily, match?.Face ?? Catalog.PreviewFace(format.FontFamily)), format.IsBold, format.IsItalic);
        if (standard is not null && CoversStandard(text))
        {
            return Builtin(standard);
        }

        if (FallbackFonts.Find(Catalog, text, format) is { } fallback)
        {
            return Installed(fallback.Program, fallback.SynthesizeBold, fallback.SynthesizeItalic);
        }

        // Nothing has every character: an installed font shows the missing ones as boxes, which at least shows they are missing.
        if (program is not null)
        {
            return Installed(program, match!.SynthesizeBold, match.SynthesizeItalic);
        }

        return standard is null ? null : Builtin(standard);
    }

    /// <summary>Makes sure an installed font is embedded with every glyph of some laid out text.</summary>
    /// <param name="program">The font.</param>
    /// <param name="text">The text.</param>
    /// <param name="glyphs">Its glyphs.</param>
    /// <param name="subset">Receives the subset, which maps the font's glyphs to the codes written.</param>
    /// <returns>The font to write with, or <see langword="null"/>.</returns>
    internal PdfiumFontHandle? Embed(FontProgram program, string text, ReadOnlySpan<LaidGlyph> glyphs, out FontSubset? subset)
    {
        ref var embedded = ref CollectionsMarshal.GetValueRefOrAddDefault(_embedded, program, out _);
        embedded ??= new(Document, program);
        var handle = embedded.Prepare(text, glyphs);
        subset = embedded.Subset;
        return handle;
    }

    /// <summary>Gets the built in family closest to a family.</summary>
    /// <param name="family">The family.</param>
    /// <param name="face">An installed face of the family, which says whether it has serifs, or <see langword="null"/>.</param>
    /// <returns>The standard family.</returns>
    private static string ClosestStandard(string family, FontFace? face) =>
        face is null ? StandardFontFamilies.Closest(family, false, false) : StandardFontFamilies.Closest(family, face.IsSerif, face.IsMonospace);

    /// <summary>Writes text in an installed font, embedded.</summary>
    /// <param name="program">The installed font.</param>
    /// <param name="bold">Whether bold is drawn in.</param>
    /// <param name="italic">Whether italic is drawn in.</param>
    /// <returns>The installed font to write with.</returns>
    private static TextFont Installed(FontProgram program, bool bold, bool italic) => new(HarfBuzzShaper.For(program), program, null, bold, italic);

    /// <summary>Writes text in a built in font.</summary>
    /// <param name="standard">The built in font.</param>
    /// <returns>The built in font to write with.</returns>
    private static TextFont Builtin(StandardFontShaper standard) => new(standard, null, standard, false, false);

    /// <summary>Determines whether the built in fonts have every character of some text.</summary>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> for Windows Latin text.</returns>
    private static bool CoversStandard(string text)
    {
        foreach (var c in text)
        {
            if (c is not ('\r' or '\n') && !StandardFontShaper.CoversCharacter(c))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Finds and loads the installed face for a format's family and style.</summary>
    /// <param name="format">The format.</param>
    /// <param name="program">Receives the loaded face, or <see langword="null"/>.</param>
    /// <returns>The match, or <see langword="null"/> for a built in or missing family.</returns>
    private FontMatch? FindInstalled(TextFormat format, out FontProgram? program)
    {
        program = null;
        if (StandardFontFamilies.IsStandard(format.FontFamily) || Catalog.Find(format.FontFamily, format.IsBold, format.IsItalic) is not { } match)
        {
            return null;
        }

        program = FontProgram.Load(match.Face);
        return match;
    }

    /// <summary>Gets a built in font in a style, loading it on first use.</summary>
    /// <param name="family">The standard family.</param>
    /// <param name="bold">Whether bold.</param>
    /// <param name="italic">Whether italic.</param>
    /// <returns>The font's shaper, or <see langword="null"/>.</returns>
    private StandardFontShaper? Standard(string family, bool bold, bool italic)
    {
        var name = StandardFontFamilies.BaseFontName(family, bold, italic);
        if (_standard.TryGetValue(name, out var shaper))
        {
            return shaper;
        }

        Span<byte> terminated = stackalloc byte[BaseFontChars];
        var length = Encoding.ASCII.GetBytes(name, terminated);
        terminated[length] = 0;
        if (Load(Document, terminated) is not { } font)
        {
            return null;
        }

        shaper = new(Document, font, family, name);
        _standard[name] = shaper;
        return shaper;
    }

    /// <summary>Closes the fonts loaded for formatted text.</summary>
    private void DisposeTextFonts()
    {
        foreach (var shaper in _standard.Values)
        {
            shaper.Font.Dispose();
        }

        foreach (var embedded in _embedded.Values)
        {
            embedded.Dispose();
        }

        _standard.Clear();
        _embedded.Clear();
    }
}
