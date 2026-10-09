// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>A page's place in a rewritten page tree.</summary>
/// <param name="Kid">The entry for the page tree's /Kids: a reference, or a direct page dictionary.</param>
/// <param name="OldIndex">The page's index before the edit, or -1 for an inserted page.</param>
[DebuggerDisplay("PdfPageSlot: {OldIndex}")]
internal readonly record struct PdfPageSlot(PdfValue Kid, int OldIndex)
{
    /// <summary>Gets the label an inserted page brings with it, or <see langword="null"/> to continue the label before it.</summary>
    internal PdfPageLabel? Label { get; init; }
}
