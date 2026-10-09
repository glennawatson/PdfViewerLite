// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>The font descriptor flags of PDF 32000-1 table 123.</summary>
[Flags]
public enum FontFlags
{
    /// <summary>No flags.</summary>
    None = 0,

    /// <summary>All glyphs have the same width.</summary>
    FixedPitch = 1 << 0,

    /// <summary>Glyphs have serifs.</summary>
    Serif = 1 << 1,

    /// <summary>The font holds glyphs outside the standard Latin set.</summary>
    Symbolic = 1 << 2,

    /// <summary>Glyphs look like cursive handwriting.</summary>
    Script = 1 << 3,

    /// <summary>The font uses the standard Latin set.</summary>
    Nonsymbolic = 1 << 5,

    /// <summary>Glyphs slant.</summary>
    Italic = 1 << 6,

    /// <summary>The font has no lowercase letters.</summary>
    AllCap = 1 << 16,

    /// <summary>Lowercase letters are small capitals.</summary>
    SmallCap = 1 << 17,

    /// <summary>Bold glyphs are emboldened at small sizes.</summary>
    ForceBold = 1 << 18,
}
