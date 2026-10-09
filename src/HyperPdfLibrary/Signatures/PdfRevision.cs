// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>One revision of a file: the original save or an incremental update appended to it.</summary>
/// <param name="Index">The revision's position, 0 for the original.</param>
/// <param name="XrefOffset">Where the revision's cross-reference section starts, or -1 when the chain could not be followed.</param>
/// <param name="MarkerEnd">The position just after the revision's <c>%%EOF</c> marker.</param>
/// <param name="EndOffset">The position after the marker's line end, where the next revision starts.</param>
[DebuggerDisplay("PdfRevision: {Index} ends at {EndOffset}")]
public readonly record struct PdfRevision(int Index, long XrefOffset, long MarkerEnd, long EndOffset);
