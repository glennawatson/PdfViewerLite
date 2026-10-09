// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>How the pixels of a replacement image are laid out.</summary>
public enum PdfImagePixelFormat
{
    /// <summary>No layout.</summary>
    None = 0,

    /// <summary>One byte per pixel, 0 black to 255 white (DeviceGray).</summary>
    Gray8 = 1,

    /// <summary>Three bytes per pixel in the order red, green, blue (DeviceRGB).</summary>
    Rgb24 = 2,
}
