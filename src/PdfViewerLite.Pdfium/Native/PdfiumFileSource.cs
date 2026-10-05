// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>
/// The open file a document reads from. .NET opens it, so paths with any characters work on every platform, and it
/// lets the file be renamed and replaced while open: saving writes a new file and moves it over the old one, which
/// Windows refuses for a file PDFium opened itself. PDFium reads blocks through <see cref="Access"/> on demand.
/// </summary>
[DebuggerDisplay("PdfiumFileSource: {Length} bytes")]
internal sealed unsafe class PdfiumFileSource : IDisposable
{
    /// <summary>The file.</summary>
    private readonly SafeFileHandle _file;

    /// <summary>The handle PDFium passes back to the read callback.</summary>
    private readonly GCHandle<SafeFileHandle> _self;

    /// <summary>The native access structure PDFium keeps a pointer to.</summary>
    private readonly NativeBufferHandle _access;

    /// <summary>Initializes a new instance of the <see cref="PdfiumFileSource"/> class.</summary>
    /// <param name="path">The file path.</param>
    /// <exception cref="IOException">The file cannot be opened or is too large for PDFium on this platform.</exception>
    internal PdfiumFileSource(string path)
    {
        _file = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            Length = RandomAccess.GetLength(_file);

            // PDFium takes the length as a C unsigned long, which is 32 bits on Windows.
            if (Length > (long)Math.Min(ulong.MaxValue >> 1, CULongMax))
            {
                throw new IOException($"The file '{path}' is too large to open.");
            }

            _self = new(_file);
            var access = new FileRead(new((nuint)Length), &ReadBlock, GCHandle<SafeFileHandle>.ToIntPtr(_self));
            _access = new(MemoryMarshal.AsBytes(new ReadOnlySpan<FileRead>(in access)));
        }
        catch
        {
            _file.Dispose();
            throw;
        }
    }

    /// <summary>Gets the file's length in bytes.</summary>
    internal long Length { get; }

    /// <summary>Gets the native access structure to pass to PDFium.</summary>
    internal NativeBufferHandle Access => _access;

    /// <summary>Gets the largest value a C unsigned long holds on this platform.</summary>
    private static ulong CULongMax => sizeof(CULong) == sizeof(uint) ? uint.MaxValue : ulong.MaxValue;

    /// <summary>Closes the file. Call only once PDFium has closed the document.</summary>
    public void Dispose()
    {
        if (_access.IsClosed)
        {
            return;
        }

        _access.Dispose();
        _self.Dispose();
        _file.Dispose();
    }

    /// <summary>PDFium's read callback: fills a block from the file.</summary>
    /// <param name="parameter">The handle to the file.</param>
    /// <param name="position">Where the block starts.</param>
    /// <param name="buffer">The block.</param>
    /// <param name="size">The block size in bytes.</param>
    /// <returns>1 when the block was filled, 0 on failure.</returns>
    [UnmanagedCallersOnly]
    private static int ReadBlock(nint parameter, CULong position, byte* buffer, CULong size)
    {
        try
        {
            var file = GCHandle<SafeFileHandle>.FromIntPtr(parameter).Target;
            var block = new Span<byte>(buffer, checked((int)size.Value));
            var offset = checked((long)position.Value);
            while (!block.IsEmpty)
            {
                var read = RandomAccess.Read(file, block, offset);
                if (read <= 0)
                {
                    return 0;
                }

                block = block[read..];
                offset += read;
            }

            return 1;
        }
        catch (IOException)
        {
            return 0;
        }
        catch (ObjectDisposedException)
        {
            return 0;
        }
        catch (OverflowException)
        {
            return 0;
        }
    }
}
