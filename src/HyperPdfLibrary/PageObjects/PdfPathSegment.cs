// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PageObjects;

/// <summary>One segment of a path, in the coordinates the content stream wrote (before the object's matrix).</summary>
/// <param name="Kind">The kind of segment.</param>
/// <param name="X1">The first point's x; a rectangle's left.</param>
/// <param name="Y1">The first point's y; a rectangle's bottom.</param>
/// <param name="X2">The second point's x, or a rectangle's width.</param>
/// <param name="Y2">The second point's y, or a rectangle's height.</param>
/// <param name="X3">The third point's x.</param>
/// <param name="Y3">The third point's y.</param>
[DebuggerDisplay("PdfPathSegment: {Kind} ({X1}, {Y1})")]
public readonly record struct PdfPathSegment(PdfPathSegmentKind Kind, float X1, float Y1, float X2, float Y2, float X3, float Y3);
