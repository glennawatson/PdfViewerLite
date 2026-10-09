// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies a recorded cell: the pattern, the colour of an uncoloured pattern, and the resolution it was rasterised for.</summary>
/// <param name="Pattern">The pattern's stream dictionary.</param>
/// <param name="Rgb">The colour of an uncoloured pattern, otherwise zero.</param>
/// <param name="ScaleBucket">The power of two at or above the page units one pattern unit spans.</param>
[DebuggerDisplay("PatternKey: {Rgb:X6} scale 2^{ScaleBucket}")]
internal readonly record struct PatternKey(PdfDictionary Pattern, uint Rgb, int ScaleBucket);
