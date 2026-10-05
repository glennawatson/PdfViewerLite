// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Writes MSIX ZIP entries with independently compressed blocks using public .NET APIs.</summary>
internal sealed class MsixZipWriter : IDisposable
{
    /// <summary>Uncompressed bytes in one MSIX hash block.</summary>
    internal const int BlockLength = 65_536;

    /// <summary>The fixed ZIP local header size.</summary>
    private const int LocalHeaderLength = 30;

    /// <summary>The local file header signature.</summary>
    private const uint LocalSignature = 0x04034B50;

    /// <summary>The central directory header signature.</summary>
    private const uint CentralSignature = 0x02014B50;

    /// <summary>The end of central directory signature.</summary>
    private const uint EndSignature = 0x06054B50;

    /// <summary>The ZIP version required for DEFLATE.</summary>
    private const ushort ZipVersion = 20;

    /// <summary>The ZIP flag for UTF-8 names.</summary>
    private const ushort Utf8Flag = 0x0800;

    /// <summary>The ZIP method for raw DEFLATE.</summary>
    private const ushort DeflateMethod = 8;

    /// <summary>The local header offset of the CRC and file sizes.</summary>
    private const int CrcOffset = 14;

    /// <summary>The first DOS timestamp year.</summary>
    private const int FirstDosYear = 1980;

    /// <summary>The last DOS timestamp year.</summary>
    private const int LastDosYear = 2107;

    /// <summary>The DOS timestamp resolution in seconds.</summary>
    private const int DosSecondResolution = 2;

    /// <summary>The reflected CRC-32 polynomial used by ZIP.</summary>
    private const uint CrcPolynomial = 0xEDB88320;

    /// <summary>The number of bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The byte mask used to index the CRC table.</summary>
    private const uint ByteMask = byte.MaxValue;

    /// <summary>The precomputed CRC table.</summary>
    private static readonly uint[] CrcTable = CreateCrcTable();

    /// <summary>The destination and ZIP field writer.</summary>
    private readonly BinaryWriter _writer;

    /// <summary>The reusable uncompressed block.</summary>
    private readonly byte[] _plain = new byte[BlockLength];

    /// <summary>The reusable compressed block, including flush overhead.</summary>
    private readonly byte[] _compressed = new byte[checked((int)DeflateEncoder.GetMaxCompressedLength(BlockLength)) + LocalHeaderLength];

    /// <summary>The completed entries for the central directory.</summary>
    private readonly List<Entry> _entries = [];

    /// <summary>Whether the central directory has been completed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="MsixZipWriter"/> class.</summary>
    /// <param name="destination">The seekable package stream, owned by the caller.</param>
    internal MsixZipWriter(Stream destination) => _writer = new(destination, Encoding.UTF8, leaveOpen: true);

    /// <summary>Completes the central directory and releases the field writer.</summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        var start = checked((uint)_writer.BaseStream.Position);
        foreach (var entry in _entries)
        {
            WriteCentralHeader(entry);
        }

        var size = checked((uint)_writer.BaseStream.Position - start);
        _writer.Write(EndSignature);
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write(checked((ushort)_entries.Count));
        _writer.Write(checked((ushort)_entries.Count));
        _writer.Write(size);
        _writer.Write(start);
        _writer.Write((ushort)0);
        _writer.Dispose();
    }

    /// <summary>Writes an entry and optionally records its MSIX block hashes.</summary>
    /// <param name="name">The ZIP entry name.</param>
    /// <param name="modified">The modification time.</param>
    /// <param name="source">The uncompressed source.</param>
    /// <param name="length">The expected uncompressed length.</param>
    /// <param name="file">The optional block map file element.</param>
    /// <exception cref="InvalidDataException">The source length changes while writing.</exception>
    internal void Write(string name, DateTime modified, Stream source, long length, XElement? file)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var offset = checked((uint)_writer.BaseStream.Position);
        WriteLocalHeader(nameBytes, modified);
        if (file is not null)
        {
            file.SetAttributeValue("Size", length);
            file.SetAttributeValue("LfhSize", LocalHeaderLength + nameBytes.Length);
            file.RemoveNodes();
        }

        var start = _writer.BaseStream.Position;
        var crc = uint.MaxValue;
        long total = 0;
        int count;
        while ((count = source.ReadAtLeast(_plain, _plain.Length, throwOnEndOfStream: false)) > 0)
        {
            var block = _plain.AsSpan(0, count);
            var compressed = WriteBlock(block);
            crc = UpdateCrc(crc, block);
            total += count;
            file?.Add(
            new XElement(file.Name.Namespace + "Block", new XAttribute("Hash", Convert.ToBase64String(SHA256.HashData(block))), new XAttribute("Size", compressed)));
        }

        WriteFinalBlock();
        if (total != length)
        {
            throw new InvalidDataException($"The payload size changed while writing {name}.");
        }

        var entry = new Entry(nameBytes, modified, ~crc, checked((uint)(_writer.BaseStream.Position - start)), checked((uint)total), offset);
        PatchLocalHeader(entry);
        _entries.Add(entry);
    }

    /// <summary>Builds the standard reflected CRC-32 lookup table.</summary>
    /// <returns>The lookup table.</returns>
    private static uint[] CreateCrcTable()
    {
        var table = new uint[byte.MaxValue + 1];
        for (uint index = 0; index < table.Length; index++)
        {
            var value = index;
            for (var bit = 0; bit < ByteBits; bit++)
            {
                value = (value & 1) == 0 ? value >> 1 : (value >> 1) ^ CrcPolynomial;
            }

            table[index] = value;
        }

        return table;
    }

    /// <summary>Updates the ZIP checksum with a payload block.</summary>
    /// <param name="crc">The running checksum.</param>
    /// <param name="bytes">The uncompressed bytes.</param>
    /// <returns>The updated checksum.</returns>
    private static uint UpdateCrc(uint crc, ReadOnlySpan<byte> bytes)
    {
        foreach (var value in bytes)
        {
            crc = (crc >> ByteBits) ^ CrcTable[(crc ^ value) & ByteMask];
        }

        return crc;
    }

    /// <summary>Compresses and flushes one block with a fresh dictionary.</summary>
    /// <param name="source">The uncompressed block.</param>
    /// <returns>The compressed byte count.</returns>
    /// <exception cref="InvalidDataException">The compression buffer is insufficient.</exception>
    private long WriteBlock(ReadOnlySpan<byte> source)
    {
        using var encoder = new DeflateEncoder();
        var status = encoder.Compress(source, _compressed, out var consumed, out var written, isFinalBlock: false);
        if (status == OperationStatus.InvalidData || consumed != source.Length)
        {
            throw new InvalidDataException("The MSIX compression buffer is insufficient.");
        }

        _writer.Write(_compressed.AsSpan(0, written));
        var total = written;
        status = encoder.Flush(_compressed, out written);
        if (status is OperationStatus.InvalidData or OperationStatus.DestinationTooSmall)
        {
            throw new InvalidDataException("The MSIX flush buffer is insufficient.");
        }

        _writer.Write(_compressed.AsSpan(0, written));
        return total + written;
    }

    /// <summary>Writes the final DEFLATE marker outside the block byte counts.</summary>
    /// <exception cref="InvalidDataException">The final compression marker could not be written.</exception>
    private void WriteFinalBlock()
    {
        using var encoder = new DeflateEncoder();
        var status = encoder.Compress([], _compressed, out _, out var written, isFinalBlock: true);
        if (status != OperationStatus.Done)
        {
            throw new InvalidDataException("The MSIX DEFLATE terminator could not be written.");
        }

        _writer.Write(_compressed.AsSpan(0, written));
    }

    /// <summary>Writes a local file header with space for the final sizes.</summary>
    /// <param name="name">The UTF-8 name.</param>
    /// <param name="modified">The modification time.</param>
    private void WriteLocalHeader(byte[] name, DateTime modified)
    {
        _writer.Write(LocalSignature);
        _writer.Write(ZipVersion);
        _writer.Write(Utf8Flag);
        _writer.Write(DeflateMethod);
        WriteTimestamp(modified);
        _writer.Write(0U);
        _writer.Write(0U);
        _writer.Write(0U);
        _writer.Write(checked((ushort)name.Length));
        _writer.Write((ushort)0);
        _writer.Write(name);
    }

    /// <summary>Fills the local header's CRC and file sizes.</summary>
    /// <param name="entry">The completed entry.</param>
    private void PatchLocalHeader(in Entry entry)
    {
        var end = _writer.BaseStream.Position;
        _writer.BaseStream.Position = entry.Offset + CrcOffset;
        _writer.Write(entry.Crc);
        _writer.Write(entry.Compressed);
        _writer.Write(entry.Uncompressed);
        _writer.BaseStream.Position = end;
    }

    /// <summary>Writes an entry in the central directory.</summary>
    /// <param name="entry">The completed entry.</param>
    private void WriteCentralHeader(in Entry entry)
    {
        _writer.Write(CentralSignature);
        _writer.Write(ZipVersion);
        _writer.Write(ZipVersion);
        _writer.Write(Utf8Flag);
        _writer.Write(DeflateMethod);
        WriteTimestamp(entry.Modified);
        _writer.Write(entry.Crc);
        _writer.Write(entry.Compressed);
        _writer.Write(entry.Uncompressed);
        _writer.Write(checked((ushort)entry.Name.Length));
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write((ushort)0);
        _writer.Write(0U);
        _writer.Write(entry.Offset);
        _writer.Write(entry.Name);
    }

    /// <summary>Writes a DOS time and date.</summary>
    /// <param name="time">The modification time.</param>
    private void WriteTimestamp(DateTime time)
    {
        var year = Math.Clamp(time.Year, FirstDosYear, LastDosYear);
        _writer.Write((ushort)((time.Hour << 11) | (time.Minute << 5) | (time.Second / DosSecondResolution)));
        _writer.Write((ushort)(((year - FirstDosYear) << 9) | (time.Month << 5) | time.Day));
    }

    /// <summary>The metadata needed by the central directory.</summary>
    /// <param name="Name">The UTF-8 entry name.</param>
    /// <param name="Modified">The modification time.</param>
    /// <param name="Crc">The ZIP checksum.</param>
    /// <param name="Compressed">The compressed byte count.</param>
    /// <param name="Uncompressed">The uncompressed byte count.</param>
    /// <param name="Offset">The local header offset.</param>
    private readonly record struct Entry(byte[] Name, DateTime Modified, uint Crc, uint Compressed, uint Uncompressed, uint Offset);
}
