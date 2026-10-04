// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// Native <c>FPDF_FILEACCESS</c>: the file's length, the callback PDFium calls to read a block, and the value it
/// passes back to that callback. PDFium keeps a pointer to it for as long as the document is open.
/// </summary>
[StructLayout(LayoutKind.Sequential)]
internal readonly unsafe struct FileRead : IEquatable<FileRead>
{
    /// <summary>The file's length in bytes.</summary>
    private readonly CULong _length;

    /// <summary>The address of the read callback.</summary>
    private readonly delegate* unmanaged<nint, CULong, byte*, CULong, int> _getBlock;

    /// <summary>The value passed to the callback.</summary>
    private readonly nint _parameter;

    /// <summary>Initializes a new instance of the <see cref="FileRead"/> struct.</summary>
    /// <param name="length">The file's length in bytes.</param>
    /// <param name="getBlock">The read callback.</param>
    /// <param name="parameter">The value passed to the callback.</param>
    internal FileRead(CULong length, delegate* unmanaged<nint, CULong, byte*, CULong, int> getBlock, nint parameter)
    {
        _length = length;
        _getBlock = getBlock;
        _parameter = parameter;
    }

    /// <summary>Compares two readers.</summary>
    /// <param name="left">The first reader.</param>
    /// <param name="right">The second reader.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(FileRead left, FileRead right) => left.Equals(right);

    /// <summary>Compares two readers.</summary>
    /// <param name="left">The first reader.</param>
    /// <param name="right">The second reader.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(FileRead left, FileRead right) => !left.Equals(right);

    /// <inheritdoc/>
    public bool Equals(FileRead other) => _length.Equals(other._length) && (nint)_getBlock == (nint)other._getBlock && _parameter == other._parameter;

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is FileRead other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => HashCode.Combine(_length, (nint)_getBlock, _parameter);
}
