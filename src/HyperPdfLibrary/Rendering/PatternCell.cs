// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>One period of a tiling pattern, rasterised or kept as a picture.</summary>
/// <param name="Picture">The period in pattern space when it is too large to rasterise, otherwise null.</param>
/// <param name="Image">The rasterised period, otherwise null.</param>
/// <param name="Tile">The period's rectangle in pattern space.</param>
/// <param name="ImageToPattern">The matrix from the image's pixels to pattern space.</param>
[DebuggerDisplay("PatternCell: {Tile}")]
internal sealed record PatternCell(SKPicture? Picture, SKImage? Image, SKRect Tile, SKMatrix ImageToPattern);
