// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Owns native memory holding a PDF that PDFium reads in place, such as a layer view's copy.</summary>
internal sealed unsafe class NativeBufferHandle : SafeHandleZeroOrMinusOneIsInvalid
{
    /// <summary>Initializes a new instance of the <see cref="NativeBufferHandle"/> class with a copy of some bytes.</summary>
    /// <param name="data">The bytes to copy.</param>
    internal NativeBufferHandle(ReadOnlySpan<byte> data)
        : base(true)
    {
        var memory = NativeMemory.Alloc((nuint)Math.Max(1, data.Length));
        data.CopyTo(new(memory, data.Length));
        SetHandle((nint)memory);
        Length = data.Length;
    }

    /// <summary>Gets the byte count.</summary>
    internal int Length { get; }

    /// <inheritdoc/>
    protected override bool ReleaseHandle()
    {
        NativeMemory.Free((void*)handle);
        return true;
    }
}
