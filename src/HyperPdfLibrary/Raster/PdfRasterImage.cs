// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Raster;

/// <summary>One image a page paints, described without decoding it.</summary>
/// <param name="Width">The width in samples.</param>
/// <param name="Height">The height in samples.</param>
/// <param name="Filters">The full filter names in the order they apply, for example "JBIG2Decode"; abbreviations are expanded. Empty for raw samples.</param>
/// <param name="ColorSpace">The colour space family, for example "DeviceGray", "ICCBased" or "Indexed"; "ImageMask" for a stencil mask; empty when missing.</param>
/// <param name="BitsPerComponent">The bits per colour component; 1 for a stencil mask; 0 when missing.</param>
/// <param name="HorizontalDpi">The samples per inch across the painted width, from the transformation matrix and not counting <c>/UserUnit</c>; 0 when the painted width is 0.</param>
/// <param name="VerticalDpi">The samples per inch along the painted height; 0 when the painted height is 0.</param>
/// <param name="IsInline">Whether the image is an inline image (<c>BI</c>) rather than an XObject.</param>
/// <param name="UsesAllowedFilters">Whether every filter is Flate, CCITT, DCT, JBIG2 or JPX.</param>
[DebuggerDisplay("PdfRasterImage: {Width}x{Height} {ColorSpace} [{Filters.Length} filters] {HorizontalDpi} dpi")]
public sealed record PdfRasterImage(
    int Width,
    int Height,
    string[] Filters,
    string ColorSpace,
    int BitsPerComponent,
    float HorizontalDpi,
    float VerticalDpi,
    bool IsInline,
    bool UsesAllowedFilters);
