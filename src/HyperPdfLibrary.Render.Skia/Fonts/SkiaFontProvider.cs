// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using SkiaSharp;

namespace HyperPdfLibrary.Render.Skia.Fonts;

/// <summary>Resolves system font families and character fallbacks through Skia.</summary>
[DebuggerDisplay("SkiaFontProvider: default loaded {_default.IsValueCreated}")]
public sealed class SkiaFontProvider : IPdfFontProvider
{
    /// <summary>The shared platform default face, created only when a fallback needs it.</summary>
    private readonly Lazy<IPdfFontFace> _default = new(static () => new SkiaFontFace(SKTypeface.Default, 0, false));

    /// <inheritdoc/>
    public IPdfFontFace DefaultFace => _default.Value;

    /// <inheritdoc/>
    public IPdfFontFace? Match(string family, int weight, bool italic)
    {
        ArgumentNullException.ThrowIfNull(family);
        var slant = italic ? SKFontStyleSlant.Italic : SKFontStyleSlant.Upright;
        using var style = new SKFontStyle(weight, (int)SKFontStyleWidth.Normal, slant);
        var typeface = SKFontManager.Default.MatchFamily(family, style);
        if (typeface is null || string.IsNullOrEmpty(typeface.FamilyName) || typeface.GlyphCount == 0)
        {
            typeface?.Dispose();
            return null;
        }

        return new SkiaFontFace(typeface, weight, italic);
    }

    /// <inheritdoc/>
    public IPdfFontFace? MatchCharacter(int unicode)
    {
        var typeface = SKFontManager.Default.MatchCharacter(unicode);
        if (typeface is null || string.IsNullOrEmpty(typeface.FamilyName))
        {
            typeface?.Dispose();
            return null;
        }

        return new SkiaFontFace(typeface, 0, false);
    }
}
