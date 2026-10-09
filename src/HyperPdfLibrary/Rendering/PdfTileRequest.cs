// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Rendering;

/// <summary>
/// Which part of a page to render. The target is the rectangle of the full rotated page image (the page drawn at
/// <paramref name="Scale"/> pixels per point) whose top-left pixel is at (<paramref name="OffsetX"/>, <paramref name="OffsetY"/>).
/// </summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Scale">Device pixels per point.</param>
/// <param name="QuarterTurns">The extra clockwise rotation in quarter turns.</param>
/// <param name="OffsetX">The x of the target's origin in the full page image.</param>
/// <param name="OffsetY">The y of the target's origin in the full page image.</param>
/// <param name="Flags">The render options.</param>
[DebuggerDisplay("PdfTileRequest: page {PageIndex} x{Scale}")]
public readonly record struct PdfTileRequest(int PageIndex, float Scale, int QuarterTurns, int OffsetX, int OffsetY, PdfRenderFlags Flags);
