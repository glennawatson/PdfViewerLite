// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A file held in memory. The bytes are kept, not copied, and must not change. Windows point straight into them, and they
/// stay readable after disposal, as nothing is released.
/// </summary>
[DebuggerDisplay("MemoryPdfByteSource: {Length} bytes")]
public sealed class MemoryPdfByteSource : PdfByteSource
{
    /// <summary>The bytes.</summary>
    private readonly ReadOnlyMemory<byte> _memory;

    /// <summary>The backing array when the memory covers a whole array, else <see langword="null"/>.</summary>
    private readonly byte[]? _array;

    /// <summary>Initializes a new instance of the <see cref="MemoryPdfByteSource"/> class over an array.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <exception cref="ArgumentNullException"><paramref name="bytes"/> is <see langword="null"/>.</exception>
    public MemoryPdfByteSource(byte[] bytes)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        _memory = bytes;
        _array = bytes;
    }

    /// <summary>Initializes a new instance of the <see cref="MemoryPdfByteSource"/> class over memory.</summary>
    /// <param name="memory">The file's bytes.</param>
    public MemoryPdfByteSource(ReadOnlyMemory<byte> memory)
    {
        _memory = memory;
        _array = MemoryMarshal.TryGetArray(memory, out var segment) && segment.Offset == 0 && segment.Array?.Length == segment.Count
            ? segment.Array
            : null;
    }

    /// <inheritdoc/>
    public override long Length => _memory.Length;

    /// <inheritdoc/>
    internal override byte[]? WholeArray => _array;

    /// <inheritdoc/>
    private protected override int ReadCore(long offset, Span<byte> destination)
    {
        _memory.Span.Slice((int)offset, destination.Length).CopyTo(destination);
        return destination.Length;
    }

    /// <inheritdoc/>
    private protected override PdfByteLease LeaseCore(long offset, int length) => new(_memory.Span.Slice((int)offset, length));
}
