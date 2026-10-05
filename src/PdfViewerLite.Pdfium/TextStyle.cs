// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>How text written on a page looks.</summary>
/// <param name="Font">The font, owned by the caller.</param>
/// <param name="FontSize">The font size in points.</param>
/// <param name="Color">The colour as 0xRRGGBB.</param>
[DebuggerDisplay("TextStyle: {FontSize}pt {Color:X6}")]
internal readonly record struct TextStyle(PdfiumFontHandle Font, float FontSize, uint Color);
