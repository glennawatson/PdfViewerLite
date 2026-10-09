// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>The result of checking a signature's /ByteRange against the file.</summary>
/// <param name="Status">Whether the ranges are well formed.</param>
/// <param name="SignedLength">The total number of signed bytes.</param>
/// <param name="SignedEnd">The position just after the last signed byte.</param>
/// <param name="RevisionIndex">The revision the signature closes, or -1 when the signed bytes do not end at a revision.</param>
/// <param name="CoversWholeDocument">Whether the ranges start at the first byte and reach the end of the file.</param>
[DebuggerDisplay("PdfByteRangeCheck: {Status} revision {RevisionIndex}")]
public readonly record struct PdfByteRangeCheck(PdfByteRangeStatus Status, long SignedLength, long SignedEnd, int RevisionIndex, bool CoversWholeDocument)
{
    /// <summary>Gets a value indicating whether the ranges are well formed.</summary>
    public bool IsValid => Status == PdfByteRangeStatus.Valid;
}
