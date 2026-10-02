// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.InteropServices;
using System.Text;

namespace PdfViewerLite.Pdfium.Native;

/// <summary>Decodes strings returned by PDFium's two-call buffer APIs.</summary>
internal static class NativeText
{
    /// <summary>Decodes a null terminated UTF-16LE byte buffer.</summary>
    /// <param name="bytes">The bytes, possibly including a terminator.</param>
    /// <returns>The string.</returns>
    internal static string FromUtf16(ReadOnlySpan<byte> bytes)
    {
        var chars = MemoryMarshal.Cast<byte, char>(bytes[..(bytes.Length & ~1)]);
        var terminator = chars.IndexOf('\0');
        return new(terminator >= 0 ? chars[..terminator] : chars);
    }

    /// <summary>Decodes a null terminated ASCII byte buffer.</summary>
    /// <param name="bytes">The bytes, possibly including a terminator.</param>
    /// <returns>The string.</returns>
    internal static string FromAscii(ReadOnlySpan<byte> bytes)
    {
        var terminator = bytes.IndexOf((byte)0);
        return Encoding.ASCII.GetString(terminator >= 0 ? bytes[..terminator] : bytes);
    }
}
