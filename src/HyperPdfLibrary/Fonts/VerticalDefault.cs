// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>The /DW2 entry of a vertical CID font, in glyph units.</summary>
/// <param name="OriginY">The vertical origin's y, 880 by default.</param>
/// <param name="Advance">The vertical advance w1y, -1000 by default.</param>
[DebuggerDisplay("VerticalDefault: {OriginY} {Advance}")]
internal readonly record struct VerticalDefault(float OriginY, float Advance);
