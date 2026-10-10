// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Drawing;

/// <summary>Describes pixel dimensions, channel order and alpha representation.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Format">The channel order, bytes per pixel and alpha representation.</param>
[DebuggerDisplay("PdfImagePixelLayout: {Width}x{Height} {Format}")]
public readonly record struct PdfImagePixelLayout(int Width, int Height, PdfImagePixelFormat Format);
