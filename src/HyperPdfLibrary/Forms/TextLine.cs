// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A line of field text: a range of the encoded text and how wide it is drawn.</summary>
/// <param name="Start">The first code of the line.</param>
/// <param name="Length">The number of codes.</param>
/// <param name="Width">The width in points, not counting trailing spaces.</param>
[DebuggerDisplay("TextLine: {Start}+{Length} {Width}")]
internal readonly record struct TextLine(int Start, int Length, float Width);
