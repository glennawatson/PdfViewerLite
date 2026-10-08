// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Text;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Fonts;

namespace PdfViewerLite.Pdfium.Text;

/// <summary>
/// Finds an installed font with every character of some text, for text the chosen font cannot show, such as Chinese
/// typed in Helvetica. Faces in the chosen weight and slant are tried first. What was found is remembered by the first
/// character the chosen font lacked, so the same script finds its font straight away next time.
/// </summary>
internal static class FallbackFonts
{
    /// <summary>The largest font file tried, so huge collections are not read just to check them.</summary>
    private const long MaxFileBytes = 64L * 1024 * 1024;

    /// <summary>The bits of a code point's block.</summary>
    private const int BlockShift = 7;

    /// <summary>The weight from which a face counts as bold.</summary>
    private const int BoldWeight = 600;

    /// <summary>The rank added for the wrong weight, which outweighs the wrong slant.</summary>
    private const int WeightMismatch = 2;

    /// <summary>What was found for each block of characters, or <see langword="null"/> when nothing has it.</summary>
    private static readonly ConcurrentDictionary<(FontCatalog Catalog, int Block, bool Bold, bool Italic), FontFace?> Found = new();

    /// <summary>Finds an installed font with every character of some text.</summary>
    /// <param name="catalog">The installed fonts.</param>
    /// <param name="text">The text.</param>
    /// <param name="format">The format, for the weight and slant wanted.</param>
    /// <returns>The font and what is drawn in, or <see langword="null"/> when no installed font has every character.</returns>
    internal static (FontProgram Program, bool SynthesizeBold, bool SynthesizeItalic)? Find(FontCatalog catalog, string text, TextFormat format)
    {
        var block = FirstUncommonBlock(text);
        var key = (catalog, block, format.IsBold, format.IsItalic);
        if (!Found.TryGetValue(key, out var face))
        {
            face = Search(catalog, text, format);
            Found[key] = face;
        }

        return face is not null && FontProgram.Load(face) is { } program && PdfiumFonts.Covers(program, text)
            ? (program, format.IsBold && face.Weight < BoldWeight, format.IsItalic && !face.IsItalic)
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
            return program is not null && PdfiumFonts.Covers(program, text);
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
}
