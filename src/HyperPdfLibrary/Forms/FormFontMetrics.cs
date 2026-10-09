// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>The vertical measurements of a field font, in thousandths of an em.</summary>
/// <param name="Ascent">The ascent.</param>
/// <param name="Descent">The descent, a negative number.</param>
/// <param name="LineHeight">The height of a line.</param>
[DebuggerDisplay("FormFontMetrics: {Ascent} {Descent}")]
internal readonly record struct FormFontMetrics(float Ascent, float Descent, float LineHeight);
