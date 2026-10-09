// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.TextLayer;

/// <summary>A word to write into an invisible text layer, placed by its box in viewer space.</summary>
/// <param name="Text">The word.</param>
/// <param name="Left">The left edge in viewer space (top-left points, rotation applied).</param>
/// <param name="Top">The top edge in viewer space.</param>
/// <param name="Right">The right edge in viewer space.</param>
/// <param name="Bottom">The bottom edge in viewer space.</param>
/// <param name="Font">The index of the word's font in the fonts given to the writer.</param>
[DebuggerDisplay("PdfTextLayerWord: {Text}")]
public readonly record struct PdfTextLayerWord(string Text, float Left, float Top, float Right, float Bottom, int Font);
