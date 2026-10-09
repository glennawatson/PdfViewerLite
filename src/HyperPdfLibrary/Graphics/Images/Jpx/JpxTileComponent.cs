// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One component of a tile: its area and the coding choices that shape its decoding.</summary>
/// <param name="Area">The tile-component's area in the component's sample grid.</param>
/// <param name="FirstResolution">The index of its lowest resolution level.</param>
/// <param name="Levels">The number of decomposition levels; there is one more resolution.</param>
/// <param name="Reversible">Whether the 5/3 reversible path is used.</param>
/// <param name="RoiShift">The region-of-interest up-shift, or zero.</param>
/// <param name="BlockStyle">The code-block mode switches.</param>
internal readonly record struct JpxTileComponent(JpxRectangle Area, int FirstResolution, int Levels, bool Reversible, int RoiShift, JpxBlockStyle BlockStyle);
