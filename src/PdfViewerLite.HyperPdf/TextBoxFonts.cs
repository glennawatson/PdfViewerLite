// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using System.Text;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;

namespace PdfViewerLite.HyperPdf;

/// <summary>
/// Chooses the font a text box is written in, as the PDFium engine chooses it: the chosen installed family when it has
/// every character; else the closest built in family when that has every character; else the first installed font
/// that has them all; else whatever was found, showing missing characters as boxes.
/// </summary>
internal static class TextBoxFonts
{
    /// <summary>The largest font file tried as a fallback, so huge collections are not read just to check them.</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>The bits of a code point's block.</summary>
    private const int BlockShift = 7;

    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The rank added for the wrong weight, which outweighs the wrong slant.</summary>
    private const int WeightMismatch = 2;

    /// <summary>The shaper of each installed font, made once.</summary>
    private static readonly ConcurrentDictionary<FontProgram, FontProgramShaper> Shapers = new();

    /// <summary>The fallback found for each block of characters, or <see langword="null"/> when nothing has it.</summary>
    private static readonly ConcurrentDictionary<FallbackKey, FontFace?> Found = new();

    /// <summary>Chooses the font to write text in.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="format">The format.</param>
    /// <param name="text">The text.</param>
    /// <returns>The font, or <see langword="null"/> when none can be used.</returns>
    internal static TextBoxFont? Resolve(FontCatalog catalog, TextFormat format, string text)
    {
        var match = StandardFontFamilies.IsStandard(format.FontFamily) ? null : catalog.Find(format.FontFamily, format.IsBold, format.IsItalic);
        var program = match is null ? null : FontProgram.Load(match.Face);
        if (program is not null && Covers(program, text))
        {
            return Installed(program, match!.SynthesizeBold, match.SynthesizeItalic);
        }

        var standard = ClosestStandard(catalog, format, match);
        if (StandardTextShaper.CoversText(text))
        {
            return new(standard, null, standard, false, false);
        }

        if (FindFallback(catalog, text, format) is { } fallback)
        {
            return fallback;
        }

        return program is not null ? Installed(program, match!.SynthesizeBold, match.SynthesizeItalic) : new(standard, null, standard, false, false);
    }

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

    /// <summary>Gets the built in font closest to a format's family, judged by the installed face when there is one.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="format">The format.</param>
    /// <param name="match">The installed face found, or <see langword="null"/>.</param>
    /// <returns>The built in font's shaper.</returns>
    private static StandardTextShaper ClosestStandard(FontCatalog catalog, TextFormat format, FontMatch? match)
    {
        var face = match?.Face ?? catalog.PreviewFace(format.FontFamily);
        var family = face is null ? StandardFontFamilies.Closest(format.FontFamily, false, false) : StandardFontFamilies.Closest(format.FontFamily, face.IsSerif, face.IsMonospace);
        return StandardTextShaper.For(family, format.IsBold, format.IsItalic);
    }

    /// <summary>Writes text in an installed font, embedded.</summary>
    /// <param name="program">The font.</param>
    /// <param name="bold">Whether bold is drawn in.</param>
    /// <param name="italic">Whether italic is drawn in.</param>
    /// <returns>The font to write with.</returns>
    private static TextBoxFont Installed(FontProgram program, bool bold, bool italic) =>
        new(Shapers.GetOrAdd(program, static font => new(font)), program, null, bold, italic);

    /// <summary>Finds an installed font with every character, remembering what was found for the text's script.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format, for the weight and slant wanted.</param>
    /// <returns>The font, or <see langword="null"/> when no installed font has every character.</returns>
    private static TextBoxFont? FindFallback(FontCatalog catalog, string text, TextFormat format)
    {
        var key = new FallbackKey(catalog, FirstUncommonBlock(text), format.IsBold, format.IsItalic);
        if (!Found.TryGetValue(key, out var face))
        {
            face = Search(catalog, text, format);
            Found[key] = face;
        }

        return face is not null && FontProgram.Load(face) is { } program && Covers(program, text)
            ? Installed(program, format.IsBold && face.Weight < BoldWeight, format.IsItalic && !face.IsItalic)
            : null;
    }

    /// <summary>Gets the block of the first character outside Latin-1, which decides which fonts can show the text.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The block number.</returns>
    private static int FirstUncommonBlock(string text)
    {
        foreach (var rune in text.EnumerateRunes())
        {
            if (rune.Value > byte.MaxValue)
            {
                return rune.Value >> BlockShift;
            }
        }

        return 0;
    }

    /// <summary>Tries the installed faces, those in the wanted style first.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format.</param>
    /// <returns>The first face with every character, or <see langword="null"/>.</returns>
    private static FontFace? Search(FontCatalog catalog, string text, TextFormat format)
    {
        var faces = new List<FontFace>(catalog.Faces);
        faces.Sort((a, b) => Rank(a, format).CompareTo(Rank(b, format)));
        foreach (var face in faces)
        {
            if (TryCovers(face, text))
            {
                return face;
            }
        }

        return null;
    }

    /// <summary>Ranks a face for a format; lower is tried first.</summary>
    /// <param name="face">The face.</param>
    /// <param name="format">The format.</param>
    /// <returns>The rank.</returns>
    private static int Rank(FontFace face, TextFormat format) =>
        (face.IsBold == format.IsBold ? 0 : WeightMismatch) + (face.IsItalic == format.IsItalic ? 0 : 1);

    /// <summary>Reads a face, without keeping it, to see whether it has every character.</summary>
    /// <param name="face">The face.</param>
    /// <param name="text">The text.</param>
    /// <returns><see langword="true"/> when it has them all.</returns>
    private static bool TryCovers(FontFace face, string text)
    {
        try
        {
            var file = new FileInfo(face.Path);
            if (!file.Exists || file.Length > MaxFileBytes)
            {
                return false;
            }

            var program = FontProgram.FromBytes(face, File.ReadAllBytes(face.Path));
            return program is not null && Covers(program, text);
        }
        catch (IOException)
        {
            return false;
        }
        catch (UnauthorizedAccessException)
        {
            return false;
        }
    }

    /// <summary>What a fallback search is remembered by.</summary>
    /// <param name="Catalog">The installed fonts.</param>
    /// <param name="Block">The block of the first character outside Latin-1.</param>
    /// <param name="Bold">Whether bold is wanted.</param>
    /// <param name="Italic">Whether italic is wanted.</param>
    [DebuggerDisplay("FallbackKey: {Block} {Bold} {Italic}")]
    private readonly record struct FallbackKey(FontCatalog Catalog, int Block, bool Bold, bool Italic);
}
