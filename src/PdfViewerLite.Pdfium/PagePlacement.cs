// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Pdfium;

/// <summary>Where a page is drawn in a bitmap, and how.</summary>
/// <param name="X">The left of the page in the bitmap.</param>
/// <param name="Y">The top of the page in the bitmap.</param>
/// <param name="Width">The page width in pixels.</param>
/// <param name="Height">The page height in pixels.</param>
/// <param name="Rotation">The rotation in quarter turns.</param>
/// <param name="Flags">The PDFium render flags.</param>
[DebuggerDisplay("PagePlacement: {X},{Y} {Width}x{Height}")]
internal readonly record struct PagePlacement(int X, int Y, int Width, int Height, int Rotation, int Flags);
