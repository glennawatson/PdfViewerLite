// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>Where and how a decoded code-block is stored in its tile-component.</summary>
/// <param name="Buffer">The tile-component's coefficients: integers for 5/3, float bits for 9/7.</param>
/// <param name="Stride">The tile-component width.</param>
/// <param name="Reversible">Whether the 5/3 reversible path is used.</param>
/// <param name="RoiShift">The region-of-interest up-shift, or zero.</param>
/// <param name="Style">The code-block mode switches.</param>
internal readonly record struct JpxBlockTarget(int[] Buffer, int Stride, bool Reversible, int RoiShift, JpxBlockStyle Style);
