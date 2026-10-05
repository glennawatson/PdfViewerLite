// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// The standard fonts a document uses for text written on its pages, each loaded once on first use so writing text
/// allocates nothing after the first time. Callers hold the PDFium lock.
/// </summary>
[DebuggerDisplay("PdfiumFonts: Fonts")]
internal sealed unsafe class PdfiumFonts : IDisposable
{
    /// <summary>The font for text boxes.</summary>
    private PdfiumFontHandle? _text;

    /// <summary>The font for typed signatures.</summary>
    private PdfiumFontHandle? _signature;

    /// <summary>Initializes a new instance of the <see cref="PdfiumFonts"/> class.</summary>
    /// <param name="document">The document the fonts belong to.</param>
    internal PdfiumFonts(PdfiumDocumentHandle document) => Document = document;

    /// <summary>Gets the document the fonts belong to.</summary>
    internal PdfiumDocumentHandle Document { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        _text?.Dispose();
        _signature?.Dispose();
        _text = null;
        _signature = null;
    }

    /// <summary>Gets the font for text boxes or signatures, loading it on first use.</summary>
    /// <param name="signature">Whether the text is a typed signature.</param>
    /// <returns>The font, or <see langword="null"/> when it cannot be loaded.</returns>
    internal PdfiumFontHandle? Get(bool signature) => signature
        ? _signature ??= Load(Document, "Times-Italic"u8)
        : _text ??= Load(Document, "Helvetica"u8);

    /// <summary>Loads one of the standard 14 fonts.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The null terminated font name.</param>
    /// <returns>The font, or <see langword="null"/>.</returns>
    private static PdfiumFontHandle? Load(PdfiumDocumentHandle document, ReadOnlySpan<byte> name)
    {
        PdfiumFontHandle font;
        fixed (byte* pointer = name)
        {
            font = NativeMethods.FPDFText_LoadStandardFont(document, pointer);
        }

        if (!font.IsInvalid)
        {
            return font;
        }

        font.Dispose();
        return null;
    }
}
