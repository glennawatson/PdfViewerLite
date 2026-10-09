// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>A transition effect used when a page appears in a presentation.</summary>
/// <param name="Style">The style: Split, Blinds, Box, Wipe, Dissolve, Glitter, R (replace), Fly, Push, Cover, Uncover, Fade.</param>
/// <param name="Duration">The effect's duration in seconds.</param>
/// <param name="Dimension">Split and Blinds: H (horizontal) or V (vertical).</param>
/// <param name="Motion">Split, Box and Fly: I (inward) or O (outward).</param>
/// <param name="Direction">The direction in degrees counter-clockwise from left to right; -1 for the name None (Fly).</param>
/// <param name="Scale">Fly: the starting or ending scale.</param>
/// <param name="Rectangular">Fly: whether the area is rectangular and aligned to the page.</param>
/// <param name="AdvanceAfter">The seconds before the page advances by itself (the page's <c>/Dur</c>), or null.</param>
[DebuggerDisplay("PdfPageTransition: {Style} {Duration}s")]
public sealed record PdfPageTransition(string Style, double Duration, string Dimension, string Motion, int Direction, double Scale, bool Rectangular, double? AdvanceAfter);
