// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>The size and layout of a pixel buffer.</summary>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Components">One for grey pixels, three for RGBX pixels.</param>
[DebuggerDisplay("PixelSize: {Width}x{Height}x{Components}")]
internal readonly record struct PixelSize(int Width, int Height, int Components);
