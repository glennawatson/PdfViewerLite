// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>What recording a tiling cell needs.</summary>
/// <param name="Owner">The interpreter that selects the pattern.</param>
/// <param name="Pattern">The pattern stream.</param>
/// <param name="Uncolored">Whether the pattern is uncoloured.</param>
/// <param name="Rgb">The colour for an uncoloured pattern.</param>
[DebuggerDisplay("CellRequest: uncolored {Uncolored}")]
internal readonly record struct CellRequest(ContentInterpreter Owner, PdfStream Pattern, bool Uncolored, uint Rgb);
