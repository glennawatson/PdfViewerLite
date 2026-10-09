// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Optimizing;

/// <summary>What one kind of optimisation saved.</summary>
/// <param name="Category">The kind of optimisation.</param>
/// <param name="Count">The objects it changed.</param>
/// <param name="BytesBefore">The stored size of those objects before.</param>
/// <param name="BytesAfter">Their stored size after; zero for objects dropped.</param>
[DebuggerDisplay("PdfOptimizeSaving: {Category} {Count} objects, {BytesSaved} bytes")]
public readonly record struct PdfOptimizeSaving(PdfOptimizeCategory Category, int Count, long BytesBefore, long BytesAfter)
{
    /// <summary>Gets the bytes saved; negative when the category grew the file, as added text layers do.</summary>
    public long BytesSaved => BytesBefore - BytesAfter;
}
