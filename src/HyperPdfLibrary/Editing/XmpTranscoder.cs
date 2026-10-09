// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Text;
using System.Text.Unicode;

namespace HyperPdfLibrary.Editing;

/// <summary>Detects the encoding of an XMP packet and converts between UTF-16 and UTF-8 without building strings.</summary>
internal static class XmpTranscoder
{
    /// <summary>The first byte of a big-endian byte order mark.</summary>
    private const byte BigEndianMarkFirst = 0xFE;

    /// <summary>The second byte of a big-endian byte order mark.</summary>
    private const byte BigEndianMarkSecond = 0xFF;

    /// <summary>The byte of the less-than sign that opens every packet.</summary>
    private const byte OpenAngle = (byte)'<';

    /// <summary>The number of bytes in one UTF-16 code unit, and in a UTF-16 byte order mark.</summary>
    private const int UnitBytes = 2;

    /// <summary>Works out a packet's encoding from its first bytes.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="markLength">The length of the byte order mark to skip: 2 for a UTF-16 mark, otherwise 0.</param>
    /// <returns>The encoding; UTF-8 unless the packet starts with a UTF-16 mark or a UTF-16 <c>&lt;</c>.</returns>
    internal static XmpEncoding Detect(ReadOnlySpan<byte> packet, out int markLength)
    {
        markLength = 0;
        if (packet.Length < UnitBytes)
        {
            return XmpEncoding.Utf8;
        }

        var first = packet[0];
        var second = packet[1];
        if (first == BigEndianMarkFirst && second == BigEndianMarkSecond)
        {
            markLength = UnitBytes;
            return XmpEncoding.Utf16BigEndian;
        }

        if (first == BigEndianMarkSecond && second == BigEndianMarkFirst)
        {
            markLength = UnitBytes;
            return XmpEncoding.Utf16LittleEndian;
        }

        if (first == 0 && second == OpenAngle)
        {
            return XmpEncoding.Utf16BigEndian;
        }

        return first == OpenAngle && second == 0 ? XmpEncoding.Utf16LittleEndian : XmpEncoding.Utf8;
    }

    /// <summary>Converts UTF-16 bytes to UTF-8.</summary>
    /// <param name="utf16">The UTF-16 bytes, without any leading byte order mark.</param>
    /// <param name="encoding">The byte order of <paramref name="utf16"/>.</param>
    /// <returns>The UTF-8 bytes, or <see langword="null"/> when the input has an odd length or invalid surrogates.</returns>
    internal static byte[]? ToUtf8(ReadOnlySpan<byte> utf16, XmpEncoding encoding)
    {
        if (utf16.Length % UnitBytes != 0)
        {
            return null;
        }

        var units = new char[utf16.Length / UnitBytes];
        for (var i = 0; i < units.Length; i++)
        {
            var pair = utf16.Slice(i * UnitBytes, UnitBytes);
            units[i] = (char)(encoding == XmpEncoding.Utf16BigEndian ? BinaryPrimitives.ReadUInt16BigEndian(pair) : BinaryPrimitives.ReadUInt16LittleEndian(pair));
        }

        var buffer = new byte[Encoding.UTF8.GetMaxByteCount(units.Length)];
        return Utf8.FromUtf16(units, buffer, out _, out var written, false) == OperationStatus.Done ? buffer.AsSpan(0, written).ToArray() : null;
    }

    /// <summary>Converts UTF-8 bytes to UTF-16, with a byte order mark when the original had one.</summary>
    /// <param name="utf8">The UTF-8 bytes.</param>
    /// <param name="encoding">The byte order to write.</param>
    /// <param name="mark">The original's leading byte order mark, or empty for none; it is written back as it was.</param>
    /// <returns>The UTF-16 bytes, or <see langword="null"/> when the input is not valid UTF-8.</returns>
    internal static byte[]? FromUtf8(ReadOnlySpan<byte> utf8, XmpEncoding encoding, ReadOnlySpan<byte> mark)
    {
        var units = new char[utf8.Length];
        if (Utf8.ToUtf16(utf8, units, out _, out var written, false) != OperationStatus.Done)
        {
            return null;
        }

        var result = new byte[mark.Length + (written * UnitBytes)];
        mark.CopyTo(result);
        for (var i = 0; i < written; i++)
        {
            var pair = result.AsSpan(mark.Length + (i * UnitBytes), UnitBytes);
            if (encoding == XmpEncoding.Utf16BigEndian)
            {
                BinaryPrimitives.WriteUInt16BigEndian(pair, units[i]);
            }
            else
            {
                BinaryPrimitives.WriteUInt16LittleEndian(pair, units[i]);
            }
        }

        return result;
    }
}
