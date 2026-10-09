// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The parameters of one scan.</summary>
/// <param name="Mode">What the scan carries.</param>
/// <param name="Start">The first zigzag index of the band.</param>
/// <param name="End">The last zigzag index of the band.</param>
/// <param name="Low">The successive-approximation bit position.</param>
internal readonly record struct JpegScan(JpegScanMode Mode, int Start, int End, int Low);
