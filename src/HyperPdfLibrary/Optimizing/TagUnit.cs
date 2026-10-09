// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>One page-level operator that inferred tagging wraps in marked content.</summary>
/// <param name="Start">The offset where the operator's operands start in the page content.</param>
/// <param name="End">The offset after the operator.</param>
/// <param name="Kind">What the operator draws.</param>
[DebuggerDisplay("TagUnit: {Kind} {Start}..{End}")]
internal readonly record struct TagUnit(int Start, int End, TagUnitKind Kind);
