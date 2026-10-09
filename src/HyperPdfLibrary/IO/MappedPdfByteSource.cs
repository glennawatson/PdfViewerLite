// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.IO;

/// <summary>
/// A file mapped read-only into memory. Windows point straight into the mapping, so nothing is copied; the operating
/// system pages the file in as it is read. Each window holds a reference on the mapping, so disposing while a window is in
/// use unmaps only once the window is released, and later reads throw <see cref="ObjectDisposedException"/>.
/// </summary>
/// <remarks>
/// The file is opened sharing read, write and delete, so other programs can still replace it. Truncating a mapped file in
/// place makes reads past the new end fault; replace files by writing a new one and moving it over the old.
/// </remarks>
[DebuggerDisplay("MappedPdfByteSource: {Length} bytes")]
public sealed unsafe class MappedPdfByteSource : PdfByteSource
{
    /// <summary>The mapping.</summary>
    private readonly MemoryMappedFile _map;

    /// <summary>The view of the whole file.</summary>
    private readonly MemoryMappedViewAccessor _view;

    /// <summary>The view's handle, reference-counted by every window.</summary>
    private readonly SafeMemoryMappedViewHandle _handle;

    /// <summary>The first byte of the file in the view.</summary>
    private readonly byte* _start;

    /// <summary>Initializes a new instance of the <see cref="MappedPdfByteSource"/> class.</summary>
    /// <param name="map">The mapping, which the source owns.</param>
    /// <param name="length">The file length.</param>
    private MappedPdfByteSource(MemoryMappedFile map, long length)
    {
        _map = map;
        Length = length;
        _view = map.CreateViewAccessor(0, length, MemoryMappedFileAccess.Read);
        _handle = _view.SafeMemoryMappedViewHandle;
        byte* pointer = null;
        _handle.AcquirePointer(ref pointer);
        _start = pointer + _view.PointerOffset;
    }

    /// <inheritdoc/>
    public override long Length { get; }

    /// <summary>Maps a file.</summary>
    /// <param name="path">The file path.</param>
    /// <returns>The source.</returns>
    /// <exception cref="IOException">The file cannot be opened or mapped, or is empty.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    public static MappedPdfByteSource Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        using var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        return Open(handle);
    }

    /// <summary>Maps an open file. The handle may be closed afterwards; the mapping keeps its own reference.</summary>
    /// <param name="handle">The file handle, opened for reading.</param>
    /// <returns>The source.</returns>
    /// <exception cref="IOException">The file cannot be mapped, or is empty.</exception>
    public static MappedPdfByteSource Open(SafeFileHandle handle)
    {
        ArgumentNullException.ThrowIfNull(handle);
        var length = RandomAccess.GetLength(handle);
        if (length <= 0)
        {
            throw new IOException("An empty file cannot be mapped.");
        }

        var map = MemoryMappedFile.CreateFromFile(handle, null, 0, MemoryMappedFileAccess.Read, HandleInheritability.None, true);
        try
        {
            return new(map, length);
        }
        catch
        {
            map.Dispose();
            throw;
        }
    }

    /// <inheritdoc/>
    protected override void Dispose(bool disposing)
    {
        if (disposing)
        {
            // The view unmaps once the last window releases its reference.
            _handle.ReleasePointer();
            _view.Dispose();
            _map.Dispose();
        }

        base.Dispose(disposing);
    }

    /// <inheritdoc/>
    private protected override int ReadCore(long offset, Span<byte> destination)
    {
        var added = false;
        _handle.DangerousAddRef(ref added);
        try
        {
            new ReadOnlySpan<byte>(_start + offset, destination.Length).CopyTo(destination);
            return destination.Length;
        }
        finally
        {
            _handle.DangerousRelease();
        }
    }

    /// <inheritdoc/>
    private protected override PdfByteLease LeaseCore(long offset, int length)
    {
        var added = false;
        _handle.DangerousAddRef(ref added);
        return new(new ReadOnlySpan<byte>(_start + offset, length), _handle);
    }
}
