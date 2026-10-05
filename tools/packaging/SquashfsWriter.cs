// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.InteropServices;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Writes a SquashFS 4 image with .NET only.</summary>
internal static class SquashfsWriter
{
    /// <summary>The SquashFS magic number.</summary>
    private const uint Magic = 0x73717368;

    /// <summary>The data block size.</summary>
    private const int BlockSize = 131_072;

    /// <summary>The zlib quality; 9 is the maximum accepted by <see cref="ZLibEncoder"/>.</summary>
    private const int CompressionQuality = 9;

    /// <summary>The number of bits to shift an inode reference to get its metadata block.</summary>
    private const int InodeBlockShift = 16;

    /// <summary>The mask that extracts the offset within the metadata block from an inode reference.</summary>
    private const long InodeOffsetMask = 0xFFFF;

    /// <summary>The SquashFS major version.</summary>
    private const ushort VersionMajor = 4;

    /// <summary>The number of distinct user and group ids stored in the id table.</summary>
    private const ushort IdCount = 1;

    /// <summary>The link count every directory has before its subdirectories are added.</summary>
    private const uint DirectoryLinkBase = 2;

    /// <summary>The bias SquashFS adds to a directory listing size.</summary>
    private const int DirectorySizeBias = 3;

    /// <summary>The base two logarithm of the block size.</summary>
    private const ushort BlockLog = 17;

    /// <summary>The metadata block size.</summary>
    private const int MetadataSize = 8192;

    /// <summary>Metadata block bytes on disk including the two byte header.</summary>
    private const int MetadataStride = MetadataSize + sizeof(ushort);

    /// <summary>The metadata header bit that marks an uncompressed block.</summary>
    private const ushort MetadataUncompressed = 0x8000;

    /// <summary>The data block size bit that marks an uncompressed block.</summary>
    private const uint DataUncompressed = 1U << 24;

    /// <summary>The compression identifier for zlib.</summary>
    private const ushort CompressionZlib = 1;

    /// <summary>The superblock flags: uncompressed metadata, no fragments and no extended attributes.</summary>
    private const ushort Flags = 0x1 | 0x8 | 0x10 | 0x100 | 0x200 | 0x800;

    /// <summary>The superblock length.</summary>
    private const int SuperblockLength = 96;

    /// <summary>The image padding boundary.</summary>
    private const int PadBoundary = 4096;

    /// <summary>The inode type of a directory.</summary>
    private const ushort InodeDirectory = 1;

    /// <summary>The inode type of a regular file.</summary>
    private const ushort InodeFile = 2;

    /// <summary>The inode type of a symbolic link.</summary>
    private const ushort InodeSymlink = 3;

    /// <summary>The value for an absent fragment.</summary>
    private const uint NoFragment = 0xFFFFFFFF;

    /// <summary>The value for an absent table.</summary>
    private const ulong NoTable = ulong.MaxValue;

    /// <summary>The permission bit mask stored in an inode.</summary>
    private const int PermissionMask = 0xFFF;

    /// <summary>The most entries allowed under one directory header.</summary>
    private const int MaxHeaderEntries = 256;

    /// <summary>The largest directory listing a basic directory inode can describe.</summary>
    private const int MaxListing = ushort.MaxValue - DirectorySizeBias;

    /// <summary>The permission bits of an implicit directory.</summary>
    private const int DirectoryMode = 0b1_1110_1101;

    /// <summary>Writes an image at the current position of a seekable stream.</summary>
    /// <param name="output">The destination.</param>
    /// <param name="entries">The entries; missing parent directories are created.</param>
    /// <param name="time">The modification time for every inode.</param>
    /// <exception cref="InvalidDataException">An entry kind is unsupported or a directory is too large.</exception>
    internal static void Write(Stream output, IReadOnlyList<PayloadEntry> entries, DateTimeOffset time)
    {
        var root = BuildTree(entries);
        using var state = new ImageState(output, output.Position, (uint)time.ToUnixTimeSeconds());
        output.Write(new byte[SuperblockLength]);

        Number(root, state);
        WriteNode(state, root, state.InodeCount + 1);

        var inodeStart = state.Position;
        WriteMetadata(output, state.Inodes);
        var directoryStart = state.Position;
        WriteMetadata(output, state.Directories);

        var idBlock = state.Position;
        Span<byte> idBytes = stackalloc byte[sizeof(ushort) + sizeof(uint)];
        BinaryPrimitives.WriteUInt16LittleEndian(idBytes, MetadataUncompressed | sizeof(uint));
        output.Write(idBytes);
        var idTable = state.Position;
        Span<byte> idIndex = stackalloc byte[sizeof(ulong)];
        BinaryPrimitives.WriteUInt64LittleEndian(idIndex, (ulong)idBlock);
        output.Write(idIndex);

        var used = state.Position;
        output.Write(new byte[(PadBoundary - (int)(used % PadBoundary)) % PadBoundary]);
        var end = output.Position;

        var superblock = new Superblock(
            Magic,
            state.InodeCount,
            state.Time,
            BlockSize,
            0,
            CompressionZlib,
            BlockLog,
            Flags,
            IdCount,
            VersionMajor,
            0,
            (ulong)root.InodeReference,
            (ulong)used,
            (ulong)idTable,
            NoTable,
            (ulong)inodeStart,
            (ulong)directoryStart,
            NoTable,
            NoTable);
        output.Position = state.Base;
        output.Write(MemoryMarshal.AsBytes(new ReadOnlySpan<Superblock>(in superblock)));
        output.Position = end;
    }

    /// <summary>Builds the directory tree.</summary>
    /// <param name="entries">The entries.</param>
    /// <returns>The root directory.</returns>
    private static Node BuildTree(IReadOnlyList<PayloadEntry> entries)
    {
        var root = new Node(string.Empty, PayloadKind.Directory, DirectoryMode, null, null);
        foreach (var entry in entries)
        {
            var segments = entry.Path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var parent = root;
            for (var index = 0; index < segments.Length - 1; index++)
            {
                if (!parent.Children.TryGetValue(segments[index], out var next))
                {
                    next = new(segments[index], PayloadKind.Directory, DirectoryMode, null, null);
                    parent.Children[segments[index]] = next;
                }

                parent = next;
            }

            var name = segments[^1];
            if (entry.Kind == PayloadKind.Directory && parent.Children.TryGetValue(name, out var existing))
            {
                existing.Mode = entry.Mode;
                continue;
            }

            parent.Children[name] = new(name, entry.Kind, entry.Mode, entry.Source, entry.Target);
        }

        return root;
    }

    /// <summary>Numbers inodes in pre-order.</summary>
    /// <param name="node">The node.</param>
    /// <param name="state">The image state.</param>
    private static void Number(Node node, ImageState state)
    {
        node.InodeNumber = ++state.InodeCount;
        foreach (var child in node.Children.Values)
        {
            Number(child, state);
        }
    }

    /// <summary>Writes a node after all of its children.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="node">The node.</param>
    /// <param name="parentInode">The parent inode number.</param>
    /// <exception cref="InvalidDataException">The entry kind is unsupported.</exception>
    private static void WriteNode(ImageState state, Node node, uint parentInode)
    {
        switch (node.Kind)
        {
            case PayloadKind.Directory:
            {
                WriteDirectory(state, node, parentInode);
                break;
            }

            case PayloadKind.File:
            {
                WriteFile(state, node);
                break;
            }

            case PayloadKind.Symlink:
            {
                WriteSymlink(state, node);
                break;
            }

            default:
                throw new InvalidDataException($"Unsupported entry kind {node.Kind}.");
        }
    }

    /// <summary>Writes a directory listing and inode.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="node">The directory.</param>
    /// <param name="parentInode">The parent inode number.</param>
    /// <exception cref="InvalidDataException">The directory listing is too large.</exception>
    private static void WriteDirectory(ImageState state, Node node, uint parentInode)
    {
        uint subdirectories = 0;
        foreach (var child in node.Children.Values)
        {
            WriteNode(state, child, node.InodeNumber);
            if (child.Kind == PayloadKind.Directory)
            {
                subdirectories++;
            }
        }

        var children = new Node[node.Children.Count];
        node.Children.Values.CopyTo(children, 0);

        var listingStart = state.Directories.Position;
        for (var position = 0; position < children.Length;)
        {
            position += WriteListingGroup(state.DirectoryWriter, children, position);
        }

        var listingLength = state.Directories.Position - listingStart;
        if (listingLength > MaxListing)
        {
            throw new InvalidDataException($"The directory {node.Name} has too many entries.");
        }

        BeginInode(state, node, InodeDirectory);
        var inodes = state.InodeWriter;
        inodes.Write((uint)(listingStart / MetadataSize * MetadataStride));
        inodes.Write(subdirectories + DirectoryLinkBase);
        inodes.Write((ushort)(listingLength + DirectorySizeBias));
        inodes.Write((ushort)(listingStart % MetadataSize));
        inodes.Write(parentInode);
    }

    /// <summary>Writes a regular file's data blocks and inode.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="node">The file.</param>
    /// <exception cref="InvalidDataException">The file is larger than 4 GiB.</exception>
    private static void WriteFile(ImageState state, Node node)
    {
        var length = new FileInfo(node.Source!).Length;
        if (length > uint.MaxValue)
        {
            throw new InvalidDataException($"{node.Source} is too large.");
        }

        var start = length == 0 ? 0 : state.Position;
        List<uint> sizes = [];
        using (var input = File.OpenRead(node.Source!))
        {
            var buffer = new byte[BlockSize];
            int read;
            while ((read = input.ReadAtLeast(buffer, BlockSize, false)) > 0)
            {
                sizes.Add(WriteBlock(state, buffer.AsSpan(0, read)));
            }
        }

        BeginInode(state, node, InodeFile);
        var inodes = state.InodeWriter;
        inodes.Write((uint)start);
        inodes.Write(NoFragment);
        inodes.Write(0U);
        inodes.Write((uint)length);
        foreach (var size in sizes)
        {
            inodes.Write(size);
        }
    }

    /// <summary>Writes one data block, compressed when that is smaller.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="block">The block content.</param>
    /// <returns>The block size entry for the file inode.</returns>
    private static uint WriteBlock(ImageState state, ReadOnlySpan<byte> block)
    {
        if (ZLibEncoder.TryCompress(block, state.Scratch, out var written, CompressionQuality, ZLibCompressionOptions.DefaultWindowLog2) && written < block.Length)
        {
            state.Output.Write(state.Scratch.AsSpan(0, written));
            return (uint)written;
        }

        state.Output.Write(block);
        return (uint)block.Length | DataUncompressed;
    }

    /// <summary>Writes one directory header and its entries.</summary>
    /// <param name="writer">The directory table writer.</param>
    /// <param name="children">The ordered children.</param>
    /// <param name="position">The index of the first child in the group.</param>
    /// <returns>The number of children written.</returns>
    private static int WriteListingGroup(BinaryWriter writer, Node[] children, int position)
    {
        var first = children[position];
        var blockStart = (uint)(first.InodeReference >> InodeBlockShift);
        var count = 1;
        while (position + count < children.Length && count < MaxHeaderEntries)
        {
            var next = children[position + count];
            var delta = (long)next.InodeNumber - first.InodeNumber;
            if ((uint)(next.InodeReference >> InodeBlockShift) != blockStart || Math.Abs(delta) > short.MaxValue)
            {
                break;
            }

            count++;
        }

        writer.Write((uint)(count - 1));
        writer.Write(blockStart);
        writer.Write(first.InodeNumber);
        for (var index = position; index < position + count; index++)
        {
            var child = children[index];
            var name = Encoding.UTF8.GetBytes(child.Name);
            writer.Write((ushort)(child.InodeReference & InodeOffsetMask));
            writer.Write((short)((long)child.InodeNumber - first.InodeNumber));
            writer.Write(GetInodeType(child.Kind));
            writer.Write((ushort)(name.Length - 1));
            writer.Write(name);
        }

        return count;
    }

    /// <summary>Writes a symbolic link inode.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="node">The link.</param>
    private static void WriteSymlink(ImageState state, Node node)
    {
        var target = Encoding.UTF8.GetBytes(node.Target!);
        BeginInode(state, node, InodeSymlink);
        state.InodeWriter.Write(1U);
        state.InodeWriter.Write((uint)target.Length);
        state.InodeWriter.Write(target);
    }

    /// <summary>Records the inode reference and writes the inode header.</summary>
    /// <param name="state">The image state.</param>
    /// <param name="node">The node.</param>
    /// <param name="type">The inode type.</param>
    private static void BeginInode(ImageState state, Node node, ushort type)
    {
        var position = state.Inodes.Position;
        node.InodeReference = ((position / MetadataSize * MetadataStride) << 16) | (position % MetadataSize);
        var writer = state.InodeWriter;
        writer.Write(type);
        writer.Write((ushort)(node.Mode & PermissionMask));
        writer.Write((ushort)0);
        writer.Write((ushort)0);
        writer.Write(state.Time);
        writer.Write(node.InodeNumber);
    }

    /// <summary>Gets the inode type stored in a directory entry.</summary>
    /// <param name="kind">The entry kind.</param>
    /// <returns>The inode type.</returns>
    private static ushort GetInodeType(PayloadKind kind) => kind switch
    {
        PayloadKind.Directory => InodeDirectory,
        PayloadKind.Symlink => InodeSymlink,
        _ => InodeFile,
    };

    /// <summary>Writes a byte stream as uncompressed metadata blocks.</summary>
    /// <param name="output">The image.</param>
    /// <param name="table">The table bytes.</param>
    private static void WriteMetadata(Stream output, MemoryStream table)
    {
        var bytes = table.GetBuffer();
        var length = (int)table.Length;
        Span<byte> header = stackalloc byte[sizeof(ushort)];
        for (var offset = 0; offset < length; offset += MetadataSize)
        {
            var size = Math.Min(MetadataSize, length - offset);
            BinaryPrimitives.WriteUInt16LittleEndian(header, (ushort)(MetadataUncompressed | size));
            output.Write(header);
            output.Write(bytes, offset, size);
        }
    }

    /// <summary>The 96 byte SquashFS superblock; only the field order matters, so the names are descriptive rather than the format's.</summary>
    /// <param name="MagicNumber">The magic number.</param>
    /// <param name="TotalInodes">The total inode count.</param>
    /// <param name="ModifiedAt">The modification time in seconds since the Unix epoch.</param>
    /// <param name="DataBlockSize">The data block size in bytes.</param>
    /// <param name="TotalFragments">The total fragment count.</param>
    /// <param name="CompressionId">The compression algorithm identifier.</param>
    /// <param name="DataBlockLog2">The base two logarithm of the data block size.</param>
    /// <param name="FeatureFlags">The feature flags.</param>
    /// <param name="TotalIds">The total number of ids in the id table.</param>
    /// <param name="MajorVersion">The format major version.</param>
    /// <param name="MinorVersion">The format minor version.</param>
    /// <param name="RootInodeReference">The root directory inode reference.</param>
    /// <param name="TotalBytesUsed">The total bytes used by the image.</param>
    /// <param name="IdTableOffset">The offset of the id table.</param>
    /// <param name="XattrTableOffset">The offset of the extended attribute table.</param>
    /// <param name="InodeTableOffset">The offset of the inode table.</param>
    /// <param name="DirectoryTableOffset">The offset of the directory table.</param>
    /// <param name="FragmentTableOffset">The offset of the fragment table.</param>
    /// <param name="LookupTableOffset">The offset of the export lookup table.</param>
    [StructLayout(LayoutKind.Sequential, Pack = 1)]
    private readonly record struct Superblock(
        uint MagicNumber,
        uint TotalInodes,
        uint ModifiedAt,
        uint DataBlockSize,
        uint TotalFragments,
        ushort CompressionId,
        ushort DataBlockLog2,
        ushort FeatureFlags,
        ushort TotalIds,
        ushort MajorVersion,
        ushort MinorVersion,
        ulong RootInodeReference,
        ulong TotalBytesUsed,
        ulong IdTableOffset,
        ulong XattrTableOffset,
        ulong InodeTableOffset,
        ulong DirectoryTableOffset,
        ulong FragmentTableOffset,
        ulong LookupTableOffset);

    /// <summary>The mutable state of an image being written.</summary>
    private sealed class ImageState : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="ImageState"/> class.</summary>
        /// <param name="output">The image stream.</param>
        /// <param name="start">The stream position where the image starts.</param>
        /// <param name="time">The inode time.</param>
        internal ImageState(Stream output, long start, uint time)
        {
            Output = output;
            Base = start;
            Time = time;
            InodeWriter = new(Inodes);
            DirectoryWriter = new(Directories);
        }

        /// <summary>Gets the image stream.</summary>
        internal Stream Output { get; }

        /// <summary>Gets the stream position where the image starts.</summary>
        internal long Base { get; }

        /// <summary>Gets the inode time.</summary>
        internal uint Time { get; }

        /// <summary>Gets the inode table bytes.</summary>
        internal MemoryStream Inodes { get; } = new();

        /// <summary>Gets the directory table bytes.</summary>
        internal MemoryStream Directories { get; } = new();

        /// <summary>Gets the inode table writer.</summary>
        internal BinaryWriter InodeWriter { get; }

        /// <summary>Gets the directory table writer.</summary>
        internal BinaryWriter DirectoryWriter { get; }

        /// <summary>Gets the compression buffer, sized for the worst case of one block.</summary>
        internal byte[] Scratch { get; } = new byte[ZLibEncoder.GetMaxCompressedLength(BlockSize)];

        /// <summary>Gets or sets the number of inodes.</summary>
        internal uint InodeCount { get; set; }

        /// <summary>Gets the image length so far.</summary>
        internal long Position => Output.Position - Base;

        /// <summary>Releases the table buffers.</summary>
        public void Dispose()
        {
            InodeWriter.Dispose();
            DirectoryWriter.Dispose();
            Inodes.Dispose();
            Directories.Dispose();
        }
    }

    /// <summary>A file, directory or link in the image.</summary>
    /// <param name="name">The entry name.</param>
    /// <param name="kind">The entry kind.</param>
    /// <param name="mode">The permission bits.</param>
    /// <param name="source">The source file.</param>
    /// <param name="target">The link target.</param>
    private sealed class Node(string name, PayloadKind kind, int mode, string? source, string? target)
    {
        /// <summary>Gets the entry name.</summary>
        internal string Name { get; } = name;

        /// <summary>Gets the entry kind.</summary>
        internal PayloadKind Kind { get; } = kind;

        /// <summary>Gets or sets the permission bits.</summary>
        internal int Mode { get; set; } = mode;

        /// <summary>Gets the source file.</summary>
        internal string? Source { get; } = source;

        /// <summary>Gets the link target.</summary>
        internal string? Target { get; } = target;

        /// <summary>Gets the children ordered by name.</summary>
        internal SortedDictionary<string, Node> Children { get; } = new(StringComparer.Ordinal);

        /// <summary>Gets or sets the inode number.</summary>
        internal uint InodeNumber { get; set; }

        /// <summary>Gets or sets the inode reference: metadata block offset shifted left 16 plus the offset in the block.</summary>
        internal long InodeReference { get; set; }
    }
}
