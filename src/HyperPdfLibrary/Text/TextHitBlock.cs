// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>The bounds and original character range of one contiguous hit-test block.</summary>
/// <param name="Bounds">The normalized union, or unbounded bounds when a character has a nonfinite coordinate.</param>
/// <param name="Start">The first character index.</param>
/// <param name="Count">The number of characters.</param>
internal readonly record struct TextHitBlock(PdfRectangle Bounds, int Start, int Count);
