// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.IO;

/// <summary>
/// The first part of another source, such as an earlier revision of a file, read without copying. It owns nothing: the
/// source it views must stay open, and disposing it does nothing.
/// </summary>
/// <param name="source">The source viewed.</param>
/// <param name="prefixLength">The number of bytes from its start.</param>
[DebuggerDisplay("PrefixPdfByteSource: {Length} bytes")]
internal sealed class PrefixPdfByteSource(PdfByteSource source, long prefixLength) : PdfByteSource
{
    /// <inheritdoc/>
    public override long Length { get; } = Math.Clamp(prefixLength, 0, source.Length);

    /// <inheritdoc/>
    private protected override int ReadCore(long offset, Span<byte> destination) => source.Read(offset, destination);

    /// <inheritdoc/>
    private protected override PdfByteLease LeaseCore(long offset, int length) => source.Lease(offset, length);
}
