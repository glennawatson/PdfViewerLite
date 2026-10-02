// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// Native <c>FPDF_FILEWRITE</c> followed by a handle to the managed stream it writes to. PDFium passes a pointer to
/// the start of the structure back to the callback, which finds the stream through <see cref="Target"/>.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly unsafe struct FileWrite : IEquatable<FileWrite>
{
    /// <summary>The interface version PDFium expects.</summary>
    private const int InterfaceVersion = 1;

    /// <summary>The interface version.</summary>
    private readonly int _version;

    /// <summary>The address of the write callback.</summary>
    private readonly delegate* unmanaged<FileWrite*, void*, CULong, int> _writeBlock;

    /// <summary>The destination stream.</summary>
    private readonly GCHandle<Stream> _stream;

    /// <summary>Initializes a new instance of the <see cref="FileWrite"/> struct.</summary>
    /// <param name="writeBlock">The write callback.</param>
    /// <param name="stream">The destination stream.</param>
    internal FileWrite(delegate* unmanaged<FileWrite*, void*, CULong, int> writeBlock, GCHandle<Stream> stream)
    {
        _version = InterfaceVersion;
        _writeBlock = writeBlock;
        _stream = stream;
    }

    /// <summary>Gets the destination stream.</summary>
    internal Stream Target => _stream.Target;

    /// <summary>Compares two writers.</summary>
    /// <param name="left">The first writer.</param>
    /// <param name="right">The second writer.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FileWrite left, FileWrite right) => left.Equals(right);

    /// <summary>Compares two writers.</summary>
    /// <param name="left">The first writer.</param>
    /// <param name="right">The second writer.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FileWrite left, FileWrite right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(FileWrite other) => _version == other._version && (nint)_writeBlock == (nint)other._writeBlock && _stream.Equals(other._stream);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FileWrite other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_version, (nint)_writeBlock, _stream);
}
