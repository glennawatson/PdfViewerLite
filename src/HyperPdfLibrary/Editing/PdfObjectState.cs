// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>One object's entry in the store's edit layer at a moment, so it can be put back exactly.</summary>
/// <param name="Number">The object number.</param>
/// <param name="Kind">Whether the object was untouched, edited or deleted.</param>
/// <param name="Value">The edited value; null unless <paramref name="Kind"/> is edited.</param>
/// <param name="FreedGeneration">The generation recorded for a freed number, or -1 when none was recorded.</param>
[DebuggerDisplay("PdfObjectState: {Number} {Kind}")]
internal readonly record struct PdfObjectState(int Number, PdfObjectStateKind Kind, PdfValue Value, int FreedGeneration);
