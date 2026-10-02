// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Ocr;

namespace PdfViewerLite.Pdfium;

/// <summary>Writing recognised text onto scanned pages.</summary>
public sealed partial class PdfiumDocument : ITextLayerWriter
{
    /// <inheritdoc/>
    public int AddTextLayer(int pageIndex, ReadOnlySpan<OcrWord> words)
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (words.IsEmpty || EditablePage(pageIndex) is not { } page || _fonts.Get(false) is not { } font)
        {
            return 0;
        }

        var written = PdfiumTextLayer.Add(_handle, font, page, words);
        _ = Changed(pageIndex, written > 0);
        return written;
    }
}
