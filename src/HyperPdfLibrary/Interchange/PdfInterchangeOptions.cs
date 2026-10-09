// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Interchange;

/// <summary>Bounds for reading an interchange file.</summary>
/// <param name="MaxLength">The most bytes (FDF) or characters (XFDF) read; a longer file is rejected.</param>
[DebuggerDisplay("PdfInterchangeOptions: {MaxLength}")]
public sealed record PdfInterchangeOptions(long MaxLength)
{
    /// <summary>Gets the default bounds: the largest decoded stream the library produces.</summary>
    public static PdfInterchangeOptions Default { get; } = new(PdfLimits.MaxDecodedLength);
}
