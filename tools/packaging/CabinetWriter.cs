// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Writes a single-folder MSZIP cabinet from ordered file payloads.</summary>
internal static class CabinetWriter
{
    /// <summary>Uncompressed bytes in a cabinet data block.</summary>
    private const int BlockLength = 32_768;

    /// <summary>Cabinet header and folder record lengths.</summary>
    private const int FilesOffset = 44;

    /// <summary>The cabinet size field offset.</summary>
    private const int SizeOffset = 8;

    /// <summary>The folder record size.</summary>
    private const int FolderLength = 8;

    /// <summary>The cabinet format minor version.</summary>
    private const byte MinorVersion = 3;

    /// <summary>The first DOS timestamp year.</summary>
    private const int FirstDosYear = 1980;

    /// <summary>The last DOS timestamp year.</summary>
    private const int LastDosYear = 2107;

    /// <summary>The DOS timestamp resolution in seconds.</summary>
    private const int DosSecondResolution = 2;

    /// <summary>Writes files in the supplied order into an MSZIP cabinet.</summary>
    /// <param name="files">Cabinet entry names and source paths.</param>
    /// <param name="output">The destination cabinet.</param>
    internal static void Build(List<(string Name, string Path)> files, string output)
    {
        using var stream = File.Create(output);
        using var writer = new BinaryWriter(stream, Encoding.UTF8, true);
        WriteHeader(writer, files.Count);
        uint offset = 0;
        foreach (var (name, path) in files)
        {
            var length = checked((uint)new FileInfo(path).Length);
            WriteFile(writer, name, path, length, offset);
            offset = checked(offset + length);
        }

        var dataOffset = checked((uint)stream.Position);
        var blocks = WriteData(writer, files);
        var size = checked((uint)stream.Length);
        stream.Position = SizeOffset;
        writer.Write(size);
        stream.Position = FilesOffset - FolderLength;
        writer.Write(dataOffset);
        writer.Write(blocks);
    }

    /// <summary>Writes the cabinet and compression folder headers.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="count">The number of files.</param>
    private static void WriteHeader(BinaryWriter writer, int count)
    {
        writer.Write("MSCF"u8);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write(0U);
        writer.Write((uint)FilesOffset);
        writer.Write(0U);
        writer.Write(MinorVersion);
        writer.Write((byte)1);
        writer.Write((ushort)1);
        writer.Write(checked((ushort)count));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(0U);
        writer.Write((ushort)0);
        writer.Write((ushort)1);
    }

    /// <summary>Writes a file descriptor with DOS date and time fields.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="name">The cabinet entry name.</param>
    /// <param name="path">The source path.</param>
    /// <param name="length">The uncompressed file length.</param>
    /// <param name="offset">The offset in the uncompressed folder data.</param>
    private static void WriteFile(BinaryWriter writer, string name, string path, uint length, uint offset)
    {
        var modified = File.GetLastWriteTime(path);
        var year = Math.Clamp(modified.Year, FirstDosYear, LastDosYear);
        writer.Write(length);
        writer.Write(offset);
        writer.Write((ushort)0);
        writer.Write((ushort)(((year - FirstDosYear) << 9) | (modified.Month << 5) | modified.Day));
        writer.Write((ushort)((modified.Hour << 11) | (modified.Minute << 5) | (modified.Second / DosSecondResolution)));
        writer.Write((ushort)0xA0);
        writer.Write(Encoding.UTF8.GetBytes(name));
        writer.Write((byte)0);
    }

    /// <summary>Compresses concatenated file bytes into independent MSZIP blocks.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="files">The ordered source files.</param>
    /// <returns>The number of data blocks.</returns>
    private static ushort WriteData(BinaryWriter writer, List<(string Name, string Path)> files)
    {
        var buffer = new byte[BlockLength];
        var used = 0;
        ushort blocks = 0;
        foreach (var (_, path) in files)
        {
            using var source = File.OpenRead(path);
            int count;
            while ((count = source.Read(buffer.AsSpan(used))) > 0)
            {
                used += count;
                if (used != buffer.Length)
                {
                    continue;
                }

                WriteBlock(writer, buffer);
                blocks = checked((ushort)(blocks + 1));
                used = 0;
            }
        }

        if (used > 0)
        {
            WriteBlock(writer, buffer.AsSpan(0, used));
            blocks = checked((ushort)(blocks + 1));
        }

        return blocks;
    }

    /// <summary>Writes a compressed block with the MSZIP marker.</summary>
    /// <param name="writer">The destination writer.</param>
    /// <param name="data">The uncompressed block.</param>
    private static void WriteBlock(BinaryWriter writer, ReadOnlySpan<byte> data)
    {
        using var compressed = new MemoryStream();
        compressed.Write("CK"u8);
        using (var deflate = new DeflateStream(compressed, CompressionLevel.Optimal, true))
        {
            deflate.Write(data);
        }

        // The CAB format permits a zero checksum to indicate that no checksum is supplied.
        writer.Write(0U);
        writer.Write(checked((ushort)compressed.Length));
        writer.Write(checked((ushort)data.Length));
        writer.Write(compressed.GetBuffer().AsSpan(0, checked((int)compressed.Length)));
    }
}
