// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One glyph or recognised word placed by the layout reading order.</summary>
/// <param name="Bounds">Its box in viewer space.</param>
/// <param name="Index">Its glyph or word index.</param>
/// <param name="TextStart">Where its text starts in the shared text.</param>
/// <param name="TextLength">The length of its text.</param>
[DebuggerDisplay("LayoutItem: {Index}")]
internal readonly record struct LayoutItem(PdfViewerRect Bounds, int Index, int TextStart, int TextLength);
