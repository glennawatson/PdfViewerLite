// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>The channel order and alpha representation of an image buffer.</summary>
public enum PdfImagePixelFormat
{
    /// <summary>One opaque grayscale byte per pixel.</summary>
    Gray8 = 0,

    /// <summary>Blue, green, red and alpha bytes, with the color channels premultiplied by alpha.</summary>
    Bgra8888 = 1,

    /// <summary>Opaque red, green, blue and an ignored fourth byte.</summary>
    Rgb888x = 2,
}
