// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Optimizing;

/// <summary>How the optimised file is written: rewritten, appended to as an incremental update, or copied unchanged.</summary>
[DebuggerDisplay("WritePlan: {Mode}")]
internal sealed class WritePlan
{
    /// <summary>The bytes copied at a time when the file is copied unchanged.</summary>
    private const int CopyChunk = 1 << 16;

    /// <summary>The rewriting writer, when the file is rewritten.</summary>
    private readonly OptimizedFileWriter? _writer;

    /// <summary>The changed store, when an incremental update is appended.</summary>
    private readonly PdfObjectStore? _store;

    /// <summary>The original file, when it is copied unchanged.</summary>
    private readonly PdfByteSource? _original;

    /// <summary>Initializes a new instance of the <see cref="WritePlan"/> class.</summary>
    /// <param name="mode">How the file is written.</param>
    /// <param name="writer">The rewriting writer, or <see langword="null"/>.</param>
    /// <param name="store">The changed store, or <see langword="null"/>.</param>
    /// <param name="original">The original file, or <see langword="null"/>.</param>
    private WritePlan(PdfOptimizeMode mode, OptimizedFileWriter? writer, PdfObjectStore? store, PdfByteSource? original)
    {
        Mode = mode;
        _writer = writer;
        _store = store;
        _original = original;
    }

    /// <summary>Gets how the file is written.</summary>
    internal PdfOptimizeMode Mode { get; }

    /// <summary>Creates a plan that rewrites the file.</summary>
    /// <param name="writer">The writer.</param>
    /// <returns>The plan.</returns>
    internal static WritePlan Rewrite(OptimizedFileWriter writer) => new(PdfOptimizeMode.Rewritten, writer, null, null);

    /// <summary>Creates a plan that appends a store's changes as an incremental update.</summary>
    /// <param name="store">The changed store.</param>
    /// <returns>The plan.</returns>
    internal static WritePlan Incremental(PdfObjectStore store) => new(PdfOptimizeMode.Incremental, null, store, null);

    /// <summary>Creates a plan that copies the file unchanged.</summary>
    /// <param name="original">The file.</param>
    /// <returns>The plan.</returns>
    internal static WritePlan Copy(PdfByteSource original) => new(PdfOptimizeMode.Copied, null, null, original);

    /// <summary>Writes the file.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The bytes written.</returns>
    internal long WriteTo(Stream destination, CancellationToken cancellationToken)
    {
        if (_writer is not null)
        {
            return _writer.WriteTo(destination, cancellationToken);
        }

        using var counter = new CountingStream(destination);
        if (_store is not null)
        {
            PdfIncrementalWriter.Save(_store, counter);
            return counter.Written;
        }

        var buffer = ArrayPool<byte>.Shared.Rent(CopyChunk);
        try
        {
            for (long offset = 0; offset < _original!.Length;)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var read = _original.Read(offset, buffer.AsSpan(0, CopyChunk));
                if (read <= 0)
                {
                    break;
                }

                counter.Write(buffer, 0, read);
                offset += read;
            }

            return counter.Written;
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }

    /// <summary>Writes the file asynchronously.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the write.</param>
    /// <returns>The bytes written.</returns>
    internal async Task<long> WriteToAsync(Stream destination, CancellationToken cancellationToken)
    {
        if (_writer is not null)
        {
            return await _writer.WriteToAsync(destination, cancellationToken).ConfigureAwait(false);
        }

        var counter = new CountingStream(destination);
        await using (counter.ConfigureAwait(false))
        {
            if (_store is not null)
            {
                await PdfIncrementalWriter.SaveAsync(_store, counter, cancellationToken).ConfigureAwait(false);
                return counter.Written;
            }

            await CopyAsync(counter, cancellationToken).ConfigureAwait(false);
            return counter.Written;
        }
    }

    /// <summary>Copies the original file asynchronously.</summary>
    /// <param name="destination">The destination.</param>
    /// <param name="cancellationToken">Stops the copy.</param>
    /// <returns>A task that completes when the file is copied.</returns>
    private async Task CopyAsync(Stream destination, CancellationToken cancellationToken)
    {
        var buffer = ArrayPool<byte>.Shared.Rent(CopyChunk);
        try
        {
            for (long offset = 0; offset < _original!.Length;)
            {
                var read = _original.Read(offset, buffer.AsSpan(0, CopyChunk));
                if (read <= 0)
                {
                    break;
                }

                await destination.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                offset += read;
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(buffer);
        }
    }
}
