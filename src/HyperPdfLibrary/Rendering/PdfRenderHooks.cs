// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>Extension points for content the library does not decode itself.</summary>
public static class PdfRenderHooks
{
    /// <summary>
    /// Gets or sets the decoder for images whose codec the library does not provide (JPEG 2000 and JBIG2). It returns a
    /// premultiplied BGRA image, or <see langword="null"/> to draw nothing. While unset such images are not drawn.
    /// </summary>
    public static Func<PdfStream, SKImage?>? UnsupportedImageDecoder { get; set; }
}
