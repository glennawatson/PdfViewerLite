// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.TextLayer;

/// <summary>How far some encoded text reaches from its origin, in thousandths of an em at a font size of one.</summary>
/// <param name="Advance">How far the pen moves.</param>
/// <param name="InkLeft">The left edge of the glyphs' ink.</param>
/// <param name="InkRight">The right edge of the glyphs' ink.</param>
[DebuggerDisplay("PdfTextExtent: {InkLeft}..{InkRight} advance {Advance}")]
public readonly record struct PdfTextExtent(float Advance, float InkLeft, float InkRight);
