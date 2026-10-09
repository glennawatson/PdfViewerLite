// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A trailer entry at a moment, so an edit to the trailer can be put back.</summary>
/// <param name="Key">The key.</param>
/// <param name="Value">The raw value; null when the key was missing.</param>
[DebuggerDisplay("PdfTrailerEntry: {Key}")]
internal readonly record struct PdfTrailerEntry(PdfName Key, PdfValue Value);
