// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#if !NET11_0_OR_GREATER
namespace HyperPdfLibrary.Compat;

/// <summary>
/// A read-only stream over unmanaged bytes that notes when a reader asked for more bytes than the data held. A
/// <see cref="System.IO.Compression.DeflateStream"/> asks for more input only while it has not yet seen the final block, so
/// hitting the end of the data means the compressed stream was cut short.
/// </summary>
internal sealed unsafe class EndTrackingStream : UnmanagedMemoryStream
{
    /// <summary>Initializes a new instance of the <see cref="EndTrackingStream"/> class.</summary>
    /// <param name="pointer">The start of the bytes.</param>
    /// <param name="length">The number of bytes.</param>
    internal EndTrackingStream(byte* pointer, int length)
        : base(pointer, length)
    {
    }

    /// <summary>Gets a value indicating whether a read found no more bytes.</summary>
    internal bool EndReached { get; private set; }

    /// <inheritdoc/>
    public override int Read(byte[] buffer, int offset, int count) => Note(base.Read(buffer, offset, count), count);

    /// <inheritdoc/>
    public override int Read(Span<byte> buffer) => Note(base.Read(buffer), buffer.Length);

    /// <summary>Records an empty read.</summary>
    /// <param name="read">The bytes the read returned.</param>
    /// <param name="requested">The bytes the caller asked for.</param>
    /// <returns>The bytes read.</returns>
    private int Note(int read, int requested)
    {
        EndReached |= read == 0 && requested > 0;
        return read;
    }
}
#endif
