// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>The decoded planes and how to read them as channels.</summary>
/// <param name="Image">The decoded planes.</param>
/// <param name="Channels">The channels.</param>
/// <param name="Palette">The palette, or <see langword="null"/>.</param>
/// <param name="Width">The output width: the first channel's plane width.</param>
/// <param name="Height">The output height: the first channel's plane height.</param>
internal readonly record struct JpxSampleSource(JpxDecodedImage Image, JpxChannel[] Channels, JpxPalette? Palette, int Width, int Height);
