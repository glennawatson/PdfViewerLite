// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;

namespace PdfViewerLite.Pdfium;

/// <summary>
/// A write-only stream over a buffer rented from the shared pool, which a save writes into before its annotations are
/// finished. It grows by renting a larger buffer and returns the buffer when disposed, so saving again reuses it.
/// </summary>
[DebuggerDisplay("PooledWriteStream: {Length} bytes")]
internal sealed class PooledWriteStream : Stream
{
    /// <summary>The bytes first rented, enough for a small document.</summary>
    private const int InitialBytes = 1 << 16;

    /// <summary>How many times larger each new buffer is, so a large save rents only a few times.</summary>
    private const int GrowthFactor = 2;

    /// <summary>The buffer.</summary>
    private byte[] _buffer = ArrayPool<byte>.Shared.Rent(InitialBytes);

    /// <summary>The bytes written.</summary>
    private int _length;

    /// <inheritdoc/>
    public override bool CanRead => false;

    /// <inheritdoc/>
    public override bool CanSeek => false;

    /// <inheritdoc/>
    public override bool CanWrite => true;

    /// <inheritdoc/>
    public override long Length => _length;

    /// <inheritdoc/>
    public override long Position
    {
        get => _length;
        set => throw new NotSupportedException();
    }

    /// <summary>Gets the bytes written, valid until the stream is disposed.</summary>
    internal ReadOnlyMemory<byte> Written => _buffer.AsMemory(0, _length);

    /// <inheritdoc/>
    public override void Flush()
    {
    }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void SetLength(long value) => throw new NotSupportedException();

    /// <inheritdoc/>
    public override void Write(byte[] buffer, int offset, int count) => Write(buffer.AsSpan(offset, count));

    /// <inheritdoc/>
    public override void Write(ReadOnlySpan<byte> buffer)
    {
        var needed = checked(_length + buffer.Length);
        if (needed > _buffer.Length)
        {
            var larger = ArrayPool<byte>.Shared.Rent(Math.Max(needed, _buffer.Length * GrowthFactor));
            _buffer.AsSpan(0, _length).CopyTo(larger);
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = larger;
        }

        buffer.CopyTo(_buffer.AsSpan(_length));
        _length = needed;
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing && _buffer.Length > 0)
        {
            ArrayPool<byte>.Shared.Return(_buffer);
            _buffer = [];
            _length = 0;
        }

        base.Dispose(disposing);
    }
}
