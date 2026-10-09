// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Fonts;

/// <summary>Loads each font dictionary of a document once, through <see cref="PdfFont.Factory"/>.</summary>
internal sealed class PdfFontCache
{
    /// <summary>The loaded fonts by dictionary.</summary>
    private readonly ObjectCache<PdfDictionary, PdfFont> _fonts = new();

    /// <summary>Gets a font.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <returns>The font, or <see langword="null"/> when no factory is set or it cannot load the font.</returns>
    internal PdfFont? Get(PdfDictionary dictionary)
    {
        var factory = PdfFont.Factory;
        return factory is null ? null : _fonts.GetOrCreate(dictionary, factory, Load);
    }

    /// <summary>Forgets every loaded font. Renders that already hold a font keep using it.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => _fonts.Clear();

    /// <summary>Loads a font, treating a damaged font as missing.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    /// <param name="load">The factory.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private static PdfFont? Load(PdfDictionary dictionary, Func<PdfDictionary, PdfFont?> load)
    {
        try
        {
            return load(dictionary);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or NotSupportedException or FormatException or IndexOutOfRangeException)
        {
            return null;
        }
    }
}
