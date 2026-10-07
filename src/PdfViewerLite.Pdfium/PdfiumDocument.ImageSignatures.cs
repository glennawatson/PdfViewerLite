// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Pdfium;

/// <summary>Image signature placement.</summary>
public sealed partial class PdfiumDocument : IImageSignatureEditor
{
    /// <inheritdoc/>
    public int AddImageSignature(int pageIndex, PageRect bounds, ReadOnlySpan<byte> pixels, int width, int height)
    {
        const int channels = 4;
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(bounds.Height);
        if (pixels.Length != checked(width * height * channels))
        {
            throw new ArgumentException("The image dimensions must match its BGRA pixels.", nameof(pixels));
        }

        using var scope = PdfiumLibrary.EnterScope();
        return Changed(pageIndex, EditablePage(pageIndex) is { } page ? PdfiumAnnotations.AddImageSignature(_handle, page, bounds, pixels, width, height, Author) : -1);
    }
}
