// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A half-open range of bytes in a content stream.</summary>
/// <param name="Start">The first byte.</param>
/// <param name="End">The offset after the last byte.</param>
[DebuggerDisplay("ByteRange: {Start}..{End}")]
internal readonly record struct ByteRange(int Start, int End)
{
    /// <summary>Gets the number of bytes.</summary>
    internal int Length => End - Start;
}
