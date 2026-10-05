// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Normalizes MSIX ZIP headers before Authenticode signing.</summary>
internal static class MsixZipHeaders
{
    /// <summary>End of central directory record length.</summary>
    private const int EndRecordLength = 22;

    /// <summary>Central directory entry header length.</summary>
    private const int CentralHeaderLength = 46;

    /// <summary>Entry count offset in the end record.</summary>
    private const int EntryCountOffset = 10;

    /// <summary>Central directory offset in the end record.</summary>
    private const int DirectoryOffset = 16;

    /// <summary>Filename length offset in the central header.</summary>
    private const int NameLengthOffset = 28;

    /// <summary>Extra data length offset in the central header.</summary>
    private const int ExtraLengthOffset = 30;

    /// <summary>Comment length offset in the central header.</summary>
    private const int CommentLengthOffset = 32;

    /// <summary>Local header offset in the central header.</summary>
    private const int LocalOffset = 42;

    /// <summary>Flag field offset in the local header.</summary>
    private const int FlagsOffset = 6;

    /// <summary>CRC field offset in the local header.</summary>
    private const int CrcOffset = 14;

    /// <summary>Combined CRC and size field length.</summary>
    private const int CrcAndSizeLength = 12;

    /// <summary>End of central directory signature.</summary>
    private const uint EndSignature = 0x06054b50;

    /// <summary>Central directory entry signature.</summary>
    private const uint CentralSignature = 0x02014b50;

    /// <summary>Local file header signature.</summary>
    private const uint LocalSignature = 0x04034b50;

    /// <summary>Flag indicating CRC and sizes are stored in a data descriptor.</summary>
    private const ushort DescriptorFlag = 0x0008;

    /// <summary>Clears local CRC and size fields when the entry has a data descriptor.</summary>
    /// <param name="asset">The MSIX path.</param>
    /// <exception cref="InvalidDataException">The archive headers are invalid or require ZIP64.</exception>
    internal static void Normalize(string asset)
    {
        using var stream = File.Open(asset, FileMode.Open, FileAccess.ReadWrite);
        Span<byte> footer = stackalloc byte[EndRecordLength];
        stream.Position = stream.Length - EndRecordLength;
        stream.ReadExactly(footer);
        if (BinaryPrimitives.ReadUInt32LittleEndian(footer) != EndSignature)
        {
            throw new InvalidDataException("The MSIX ZIP end record is invalid.");
        }

        var count = BinaryPrimitives.ReadUInt16LittleEndian(footer[EntryCountOffset..]);
        var offset = BinaryPrimitives.ReadUInt32LittleEndian(footer[DirectoryOffset..]);
        if (count == ushort.MaxValue || offset == uint.MaxValue)
        {
            throw new InvalidDataException("The MSIX header normalizer requires a ZIP32 archive.");
        }

        Span<byte> central = stackalloc byte[CentralHeaderLength];
        Span<byte> local = stackalloc byte[CrcOffset];
        Span<byte> zeros = stackalloc byte[CrcAndSizeLength];
        zeros.Clear();
        stream.Position = offset;
        for (var index = 0; index < count; index++)
        {
            stream.ReadExactly(central);
            var next = stream.Position + BinaryPrimitives.ReadUInt16LittleEndian(central[NameLengthOffset..])
                + BinaryPrimitives.ReadUInt16LittleEndian(central[ExtraLengthOffset..]) + BinaryPrimitives.ReadUInt16LittleEndian(central[CommentLengthOffset..]);
            if (BinaryPrimitives.ReadUInt32LittleEndian(central) != CentralSignature)
            {
                throw new InvalidDataException("The MSIX central directory header is invalid.");
            }

            stream.Position = BinaryPrimitives.ReadUInt32LittleEndian(central[LocalOffset..]);
            stream.ReadExactly(local);
            if (BinaryPrimitives.ReadUInt32LittleEndian(local) != LocalSignature)
            {
                throw new InvalidDataException("The MSIX local file header is invalid.");
            }

            // .NET's archive updater retains these values; MSIX verification expects the descriptor to own them.
            if ((BinaryPrimitives.ReadUInt16LittleEndian(local[FlagsOffset..]) & DescriptorFlag) != 0)
            {
                stream.Write(zeros);
            }

            stream.Position = next;
        }
    }
}
