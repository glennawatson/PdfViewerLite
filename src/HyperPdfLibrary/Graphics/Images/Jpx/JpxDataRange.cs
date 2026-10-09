// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>A run of bytes within a buffer.</summary>
/// <param name="Offset">The first byte.</param>
/// <param name="Length">The number of bytes.</param>
internal readonly record struct JpxDataRange(int Offset, int Length)
{
    /// <summary>Gets the byte after the last.</summary>
    internal int End => Offset + Length;
}
