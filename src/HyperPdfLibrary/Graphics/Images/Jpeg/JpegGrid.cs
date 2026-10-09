// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The block layout of one component.</summary>
/// <param name="McusPerLine">The MCUs across the image.</param>
/// <param name="McusPerColumn">The MCUs down the image.</param>
/// <param name="UsedBlocksPerLine">The blocks across that hold image data.</param>
/// <param name="UsedBlocksPerColumn">The blocks down that hold image data.</param>
internal readonly record struct JpegGrid(int McusPerLine, int McusPerColumn, int UsedBlocksPerLine, int UsedBlocksPerColumn);
