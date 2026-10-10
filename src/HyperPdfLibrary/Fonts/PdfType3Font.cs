// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// A Type 3 font: each glyph is a content stream the interpreter runs. The font parses the dictionary; the interpreter
/// draws the glyph streams with the font matrix, so <see cref="GetOutline"/> gives nothing.
/// </summary>
[DebuggerDisplay("PdfType3Font")]
public abstract class PdfType3Font : PdfFont
{
    /// <summary>Initializes a new instance of the <see cref="PdfType3Font"/> class.</summary>
    /// <param name="dictionary">The font dictionary.</param>
    protected PdfType3Font(PdfDictionary dictionary)
        : base(dictionary)
    {
    }

    /// <inheritdoc/>
    public override bool IsType3 => true;

    /// <summary>Gets the font's own resources, or <see langword="null"/> to use those of the page.</summary>
    public abstract PdfDictionary? Resources { get; }

    /// <summary>Gets a code's glyph content stream.</summary>
    /// <param name="code">The character code.</param>
    /// <returns>The stream, or <see langword="null"/> when the code has no glyph.</returns>
    public abstract PdfStream? GetCharProc(int code);

    /// <inheritdoc/>
    public override PdfPath? GetOutline(int code) => null;
}
