// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.IO;

/// <summary>
/// The bytes of a PDF file, read by offset without loading the whole file. Implementations are safe to call from any
/// thread. See <see cref="MemoryPdfByteSource"/>, <see cref="MappedPdfByteSource"/> and <see cref="StreamPdfByteSource"/>.
/// </summary>
[DebuggerDisplay("PdfByteSource: {Length} bytes")]
public abstract class PdfByteSource : IDisposable
{
    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfByteSource"/> class.</summary>
    private protected PdfByteSource()
    {
    }

    /// <summary>Gets the number of bytes.</summary>
    public abstract long Length { get; }

    /// <summary>Gets a value indicating whether the source has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the whole file as one array starting at offset zero, or <see langword="null"/> when it is not held that way.</summary>
    internal virtual byte[]? WholeArray => null;

    /// <summary>Gets a value indicating whether a read can wait on a file or stream, so loading ahead with <see cref="PrefetchAsync"/> pays.</summary>
    internal virtual bool NeedsPrefetch => false;

    /// <summary>Gets a number that changes whenever the cache drops loaded bytes; while it is unchanged, bytes loaded ahead are still cached.</summary>
    internal virtual long CacheGeneration => 0;

    /// <summary>Gets the most bytes one operation should load ahead, so it does not push its own pages out of the cache.</summary>
    internal virtual long PrefetchBudget => 0;

    /// <summary>Copies bytes into a buffer.</summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="destination">The buffer.</param>
    /// <returns>The number of bytes copied; fewer than asked only at the end of the source.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The source holds a file or mapping that has been released.</exception>
    public int Read(long offset, Span<byte> destination)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        var available = Length - offset;
        if (available <= 0 || destination.IsEmpty)
        {
            return 0;
        }

        var count = (int)Math.Min(destination.Length, available);
        return ReadCore(offset, destination[..count]);
    }

    /// <summary>
    /// Copies bytes into a buffer, waiting for the file or stream with async I/O where the source reads one. Sources held
    /// in memory or mapped complete at once.
    /// </summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="destination">The buffer.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The number of bytes copied; fewer than asked only at the end of the source.</returns>
    /// <exception cref="ArgumentOutOfRangeException"><paramref name="offset"/> is negative.</exception>
    /// <exception cref="ObjectDisposedException">The source holds a file or mapping that has been released.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public virtual ValueTask<int> ReadAsync(long offset, Memory<byte> destination, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled<int>(cancellationToken) : new(Read(offset, destination.Span));

    /// <summary>
    /// Loads a byte range into the source's cache with async I/O, so the synchronous reads that follow find it there. A
    /// source that is already in memory completes at once.
    /// </summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="length">The number of bytes; a range past the end is cut, and one larger than the cache loads its first part.</param>
    /// <param name="cancellationToken">Cancels the loading.</param>
    /// <returns>A task that completes when the range is cached.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public virtual ValueTask PrefetchAsync(long offset, long length, CancellationToken cancellationToken) =>
        cancellationToken.IsCancellationRequested ? ValueTask.FromCanceled(cancellationToken) : ValueTask.CompletedTask;

    /// <summary>Gets a read-only window of bytes; dispose the lease when done.</summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="length">The number of bytes, which must lie inside the source.</param>
    /// <returns>The lease.</returns>
    /// <exception cref="ArgumentOutOfRangeException">The range is not inside the source.</exception>
    /// <exception cref="ObjectDisposedException">The source holds a file or mapping that has been released.</exception>
    public PdfByteLease Lease(long offset, int length)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(offset);
        ArgumentOutOfRangeException.ThrowIfNegative(length);
        ArgumentOutOfRangeException.ThrowIfGreaterThan(offset + length, Length, nameof(length));
        return length == 0 ? default : LeaseCore(offset, length);
    }

    /// <summary>Releases the file, mapping or cache behind the source.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        Dispose(true);
        GC.SuppressFinalize(this);
    }

    /// <summary>Gets the byte at an offset.</summary>
    /// <param name="offset">The offset.</param>
    /// <returns>The byte, or -1 outside the source.</returns>
    internal int ByteAt(long offset)
    {
        if (offset < 0 || offset >= Length)
        {
            return -1;
        }

        Span<byte> one = stackalloc byte[1];
        return Read(offset, one) == 1 ? one[0] : -1;
    }

    /// <summary>Releases resources.</summary>
    /// <param name="disposing">Whether called from <see cref="Dispose()"/>.</param>
    protected virtual void Dispose(bool disposing)
    {
    }

    /// <summary>Copies bytes known to lie inside the source.</summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="destination">The buffer, no longer than the bytes left.</param>
    /// <returns>The number of bytes copied.</returns>
    private protected abstract int ReadCore(long offset, Span<byte> destination);

    /// <summary>Gets a window known to lie inside the source.</summary>
    /// <param name="offset">The offset of the first byte.</param>
    /// <param name="length">The number of bytes, at least one.</param>
    /// <returns>The lease.</returns>
    private protected abstract PdfByteLease LeaseCore(long offset, int length);
}
