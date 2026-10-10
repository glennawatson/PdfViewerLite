// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Graphics.Shadings;

/// <summary>The colour stops of a sampled gradient.</summary>
/// <param name="Colors">The colours.</param>
/// <param name="Positions">The position of each colour, from 0 to 1.</param>
[DebuggerDisplay("GradientStops: {Colors.Length} stops")]
internal sealed record GradientStops(PdfColor[] Colors, float[] Positions);
