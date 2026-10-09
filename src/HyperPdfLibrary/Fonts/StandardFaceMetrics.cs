// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Fonts.Data;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The ascent and descent PDFium gives a standard 14 font that has no font box. PDFium then reads them from the face it
/// draws the font with, its built-in Foxit faces, whose CFF font box tops and bottoms are these numbers. The AFM
/// ascender and descender are smaller, so text boxes built from them would be shorter than PDFium's.
/// </summary>
internal static class StandardFaceMetrics
{
    /// <summary>The numbers stored per font: ascent then descent.</summary>
    private const int Stride = 2;

    /// <summary>
    /// Gets the ascent and descent of each standard font in <see cref="StandardFont"/> order from Courier, in thousandths:
    /// the font box tops and bottoms of FoxitFixed, FoxitFixedBold, FoxitFixedBoldItalic, FoxitFixedItalic, FoxitSans,
    /// FoxitSansBold, FoxitSansBoldItalic, FoxitSansItalic, FoxitSerif, FoxitSerifBold, FoxitSerifBoldItalic,
    /// FoxitSerifItalic, FoxitSymbol and FoxitDingbats.
    /// </summary>
    private static ReadOnlySpan<short> Table =>
    [
        0x323, -0xF9, 0x32B, -0xF9, 0x32B, -0xF9, 0x323, -0xF9,
        0x3B1, -0xE1, 0x3C2, -0xE4, 0x3C2, -0xE4, 0x3B7, -0xE1,
        0x36E, -0xFA, 0x379, -0xFA, 0x365, -0xFA, 0x364, -0xFA,
        0x3F2, -0x126, 0x33A, -0xA4,
    ];

    /// <summary>Gets the ascent PDFium gives a standard font without a font box.</summary>
    /// <param name="font">The standard font; not <see cref="StandardFont.None"/>.</param>
    /// <returns>The ascent in thousandths.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Ascent(StandardFont font) => Table[((int)font - 1) * Stride];

    /// <summary>Gets the descent PDFium gives a standard font without a font box.</summary>
    /// <param name="font">The standard font; not <see cref="StandardFont.None"/>.</param>
    /// <returns>The descent in thousandths, a negative number.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static float Descent(StandardFont font) => Table[(((int)font - 1) * Stride) + 1];
}
