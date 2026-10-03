// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.Pdfium.Native;

namespace PdfViewerLite.Pdfium;

/// <summary>Digital signatures.</summary>
public sealed partial class PdfiumDocument
{
    /// <summary>The most bytes read for a short ASCII value such as the sub-filter or time.</summary>
    private const int AsciiValueBytes = 128;

    /// <summary>The signatures, read once: a document's signatures cannot change while it is open.</summary>
    private RawSignature[]? _signatures;

    /// <inheritdoc/>
    public int SignatureCount
    {
        get
        {
            using var scope = PdfiumLibrary.EnterScope();
            return IsDisposed ? 0 : Math.Max(0, NativeMethods.FPDF_GetSignatureCount(_handle));
        }
    }

    /// <inheritdoc/>
    public unsafe IReadOnlyList<RawSignature> GetSignatures()
    {
        using var scope = PdfiumLibrary.EnterScope();
        if (_signatures is { } cached)
        {
            return cached;
        }

        var count = IsDisposed ? 0 : Math.Max(0, NativeMethods.FPDF_GetSignatureCount(_handle));
        var signatures = new RawSignature[count];
        for (var i = 0; i < count; i++)
        {
            var signature = NativeMethods.FPDF_GetSignatureObject(_handle, i);
            signatures[i] = new(i, ReadContents(signature), ReadByteRange(signature), ReadAscii(signature, &GetSubFilter), ReadReason(signature), PdfDate.Parse(ReadAscii(signature, &GetTime)));
        }

        _signatures = signatures;
        return signatures;
    }

    /// <summary>Reads a signature's encoded contents.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The bytes.</returns>
    private static unsafe byte[] ReadContents(nint signature)
    {
        var length = (int)NativeMethods.FPDFSignatureObj_GetContents(signature, null, default).Value;
        var contents = new byte[Math.Max(0, length)];
        fixed (byte* buffer = contents)
        {
            _ = NativeMethods.FPDFSignatureObj_GetContents(signature, buffer, new((uint)contents.Length));
        }

        return contents;
    }

    /// <summary>Reads a signature's byte range.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>Offset and length pairs.</returns>
    private static unsafe long[] ReadByteRange(nint signature)
    {
        var count = (int)NativeMethods.FPDFSignatureObj_GetByteRange(signature, null, default).Value;
        if (count <= 0)
        {
            return [];
        }

        var values = new int[count];
        fixed (int* buffer = values)
        {
            _ = NativeMethods.FPDFSignatureObj_GetByteRange(signature, buffer, new((uint)count));
        }

        var range = new long[count];
        for (var i = 0; i < count; i++)
        {
            range[i] = values[i];
        }

        return range;
    }

    /// <summary>Reads a signature's reason.</summary>
    /// <param name="signature">The signature.</param>
    /// <returns>The reason, or an empty string.</returns>
    private static unsafe string ReadReason(nint signature)
    {
        var length = (int)NativeMethods.FPDFSignatureObj_GetReason(signature, null, default).Value;
        if (length <= sizeof(char))
        {
            return string.Empty;
        }

        var buffer = new byte[length];
        fixed (byte* pointer = buffer)
        {
            _ = NativeMethods.FPDFSignatureObj_GetReason(signature, pointer, new((uint)length));
        }

        return NativeText.FromUtf16(buffer);
    }

    /// <summary>Reads a short ASCII value.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="read">The reader.</param>
    /// <returns>The value, or an empty string.</returns>
    private static unsafe string ReadAscii(nint signature, delegate*<nint, byte*, CULong, CULong> read)
    {
        Span<byte> buffer = stackalloc byte[AsciiValueBytes];
        fixed (byte* pointer = buffer)
        {
            var length = (int)read(signature, pointer, new((uint)buffer.Length)).Value;
            return length is <= 1 or > AsciiValueBytes ? string.Empty : NativeText.FromAscii(buffer[..length]);
        }
    }

    /// <summary>Reads a signature's sub-filter.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The needed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe CULong GetSubFilter(nint signature, byte* buffer, CULong length) => NativeMethods.FPDFSignatureObj_GetSubFilter(signature, buffer, length);

    /// <summary>Reads a signature's time.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="length">The buffer length.</param>
    /// <returns>The needed length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static unsafe CULong GetTime(nint signature, byte* buffer, CULong length) => NativeMethods.FPDFSignatureObj_GetTime(signature, buffer, length);
}
