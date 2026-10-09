// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Text;

/// <summary>A run of characters a search matched.</summary>
/// <param name="Start">The first character index.</param>
/// <param name="Count">The number of characters.</param>
[DebuggerDisplay("PdfTextMatch: [{Start}+{Count}]")]
public readonly record struct PdfTextMatch(int Start, int Count);
