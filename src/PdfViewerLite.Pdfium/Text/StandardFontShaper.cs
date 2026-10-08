// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Text;
using PdfViewerLite.Core.Text.Layout;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium.Text;

/// <summary>
/// Lays text out in one of the built in PDF fonts (Helvetica, Times or Courier). These fonts are not embedded and cover
/// only the Windows Latin characters; each character's width is measured once through PDFium, so text lines up as
/// every reader draws it. Callers hold the PDFium lock.
/// </summary>
[DebuggerDisplay("StandardFontShaper: {BaseFont}")]
internal sealed unsafe class StandardFontShaper : ITextShaper
{
    /// <summary>The size characters are measured at, so widths keep their precision.</summary>
    private const float MeasureSize = 100;

    /// <summary>The first printable ASCII character.</summary>
    private const int FirstPrintable = 0x20;

    /// <summary>The last printable ASCII character.</summary>
    private const int LastPrintable = 0x7E;

    /// <summary>The first printable Latin-1 character.</summary>
    private const int FirstLatin1 = 0xA0;

    /// <summary>The last Latin-1 character.</summary>
    private const int LastLatin1 = 0xFF;

    /// <summary>The Helvetica ascent, from its font metrics.</summary>
    private const float SansAscent = 0.718F;

    /// <summary>The Helvetica descent.</summary>
    private const float SansDescent = -0.207F;

    /// <summary>The Times ascent.</summary>
    private const float SerifAscent = 0.683F;

    /// <summary>The Times descent.</summary>
    private const float SerifDescent = -0.217F;

    /// <summary>The Courier ascent.</summary>
    private const float MonoAscent = 0.629F;

    /// <summary>The Courier descent.</summary>
    private const float MonoDescent = -0.157F;

    /// <summary>Where the underline sits in all three families.</summary>
    private const float Underline = -0.1F;

    /// <summary>The underline thickness in all three families.</summary>
    private const float UnderlineWidth = 0.05F;

    /// <summary>The character that brackets a measured one, so its ink fixes both ends.</summary>
    private const char Bracket = 'H';

    /// <summary>The Windows Latin characters above ASCII that are not Latin-1.</summary>
    private static readonly FrozenSet<int> WindowsExtras =
    [
        0x20AC, 0x201A, 0x0192, 0x201E, 0x2026, 0x2020, 0x2021, 0x02C6, 0x2030, 0x0160, 0x2039, 0x0152, 0x017D,
        0x2018, 0x2019, 0x201C, 0x201D, 0x2022, 0x2013, 0x2014, 0x02DC, 0x2122, 0x0161, 0x203A, 0x0153, 0x017E, 0x0178,
    ];

    /// <summary>The document the font belongs to.</summary>
    private readonly PdfiumDocumentHandle _document;

    /// <summary>Each measured character's advance, in ems.</summary>
    private readonly Dictionary<char, float> _advances = [];

    /// <summary>Initializes a new instance of the <see cref="StandardFontShaper"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="font">The loaded font.</param>
    /// <param name="family">The standard family.</param>
    /// <param name="baseFont">The base font name.</param>
    internal StandardFontShaper(PdfiumDocumentHandle document, PdfiumFontHandle font, string family, string baseFont)
    {
        _document = document;
        Font = font;
        BaseFont = baseFont;
        (Ascent, Descent) = Extent(family);
    }

    /// <inheritdoc/>
    public float Ascent { get; }

    /// <inheritdoc/>
    public float Descent { get; }

    /// <inheritdoc/>
    public float UnderlinePosition => Underline;

    /// <inheritdoc/>
    public float UnderlineThickness => UnderlineWidth;

    /// <summary>Gets the loaded font.</summary>
    internal PdfiumFontHandle Font { get; }

    /// <summary>Gets the base font name, such as "Times-Bold".</summary>
    internal string BaseFont { get; }

    /// <inheritdoc/>
    public bool Shape(ReadOnlySpan<char> text, List<ShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        for (var i = 0; i < text.Length; i++)
        {
            var c = text[i];
            output.Add(new(c, i, Advance(c), 0, 0));
        }

        return false;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Covers(int codePoint) => CoversCharacter(codePoint);

    /// <summary>Determines whether a built in font can show a character.</summary>
    /// <param name="codePoint">The character.</param>
    /// <returns><see langword="true"/> for the Windows Latin characters.</returns>
    internal static bool CoversCharacter(int codePoint) =>
        codePoint is (>= FirstPrintable and <= LastPrintable) or (>= FirstLatin1 and <= LastLatin1) || WindowsExtras.Contains(codePoint);

    /// <summary>Gets a standard family's ascent and descent.</summary>
    /// <param name="family">The family.</param>
    /// <returns>The ascent and descent in ems.</returns>
    private static (float Ascent, float Descent) Extent(string family)
    {
        if (family == StandardFontFamilies.Serif)
        {
            return (SerifAscent, SerifDescent);
        }

        return family == StandardFontFamilies.Mono ? (MonoAscent, MonoDescent) : (SansAscent, SansDescent);
    }

    /// <summary>Gets a character's advance, measuring it the first time.</summary>
    /// <param name="c">The character.</param>
    /// <returns>The advance in ems.</returns>
    private float Advance(char c)
    {
        if (_advances.TryGetValue(c, out var advance))
        {
            return advance;
        }

        Span<char> bracketed = [Bracket, c, Bracket, '\0'];
        Span<char> pair = [Bracket, Bracket, '\0'];
        advance = CoversCharacter(c) ? Math.Max(0, (InkWidth(bracketed) - InkWidth(pair)) / MeasureSize) : 0;
        _advances[c] = advance;
        return advance;
    }

    /// <summary>Measures the ink width of some text at the measuring size.</summary>
    /// <param name="terminated">The null terminated text.</param>
    /// <returns>The width in points.</returns>
    private float InkWidth(ReadOnlySpan<char> terminated)
    {
        var textObject = NativeMethods.FPDFPageObj_CreateTextObj(_document, Font, MeasureSize);
        if (textObject == 0)
        {
            return 0;
        }

        try
        {
            fixed (char* pointer = terminated)
            {
                _ = NativeMethods.FPDFText_SetText(textObject, pointer);
            }

            return NativeMethods.FPDFPageObj_GetBounds(textObject, out var left, out _, out var right, out _) != 0 ? right - left : 0;
        }
        finally
        {
            NativeMethods.FPDFPageObj_Destroy(textObject);
        }
    }
}
