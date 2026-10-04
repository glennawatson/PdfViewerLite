// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium <c>FS_SIZEF</c>.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[StructLayout(LayoutKind.Sequential)]
internal readonly record struct FsSizeF(float Width, float Height);
