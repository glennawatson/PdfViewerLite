// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The ascent and descent of a font, in font units.</summary>
/// <param name="Ascent">The distance from the baseline to the top.</param>
/// <param name="Descent">The distance from the baseline to the bottom, normally negative.</param>
[DebuggerDisplay("Ascent {Ascent}, descent {Descent}")]
internal readonly record struct VerticalMetrics(float Ascent, float Descent);
