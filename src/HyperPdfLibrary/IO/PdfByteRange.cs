// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.IO;

/// <summary>A run of bytes in a file.</summary>
/// <param name="Offset">The first byte.</param>
/// <param name="Length">The number of bytes.</param>
[DebuggerDisplay("PdfByteRange: {Offset}+{Length}")]
internal readonly record struct PdfByteRange(long Offset, long Length);
