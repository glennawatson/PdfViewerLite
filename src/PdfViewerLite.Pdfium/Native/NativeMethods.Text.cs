// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Writing text with embedded fonts: loading a TrueType font as a CID font, and placing glyphs by number.</summary>
internal static unsafe partial class NativeMethods
{
    /// <summary>Native <c>FPDFText_LoadCidType2Font</c> entry point: loads a TrueType font with its own text map and glyph map.</summary>
    /// <param name="document">The document.</param>
    /// <param name="fontData">The font file, copied by PDFium.</param>
    /// <param name="fontDataSize">The font file's length.</param>
    /// <param name="toUnicodeCMap">The null terminated ToUnicode CMap program.</param>
    /// <param name="cidToGidMap">The CID to glyph map, two big-endian bytes per CID.</param>
    /// <param name="cidToGidMapSize">The map's length.</param>
    /// <returns>The font, which must be closed with <see cref="FPDFFont_Close"/>.</returns>
    [LibraryImport(Library)]
    internal static partial PdfiumFontHandle FPDFText_LoadCidType2Font(PdfiumDocumentHandle document, byte* fontData, uint fontDataSize, byte* toUnicodeCMap, byte* cidToGidMap, uint cidToGidMapSize);

    /// <summary>Native <c>FPDFText_SetCharcodes</c> entry point: sets a text object's character codes.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="charcodes">The codes.</param>
    /// <param name="count">The number of codes.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_SetCharcodes(nint textObject, uint* charcodes, nuint count);

    /// <summary>Native <c>FPDFText_SetPositions</c> entry point: places every character after the first, in points from the object's origin.</summary>
    /// <param name="textObject">The text object.</param>
    /// <param name="positions">The positions of the second character on.</param>
    /// <param name="count">The number of positions, one less than the characters.</param>
    /// <returns>Non-zero on success.</returns>
    [LibraryImport(Library)]
    internal static partial int FPDFText_SetPositions(nint textObject, float* positions, nuint count);
}
