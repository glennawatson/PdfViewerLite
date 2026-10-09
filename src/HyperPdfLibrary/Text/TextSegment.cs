// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>A run of indexes: a bidi segment of a line, or a run of characters that have text.</summary>
/// <param name="Start">The first index.</param>
/// <param name="Count">The number of indexes.</param>
/// <param name="Direction">The segment's direction; neutral for character runs.</param>
[DebuggerDisplay("TextSegment: [{Start}+{Count}] {Direction}")]
internal readonly record struct TextSegment(int Start, int Count, TextDirection Direction);
