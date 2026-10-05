// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Builds RPM packages with .NET only.</summary>
internal static class RpmBuilder
{
    /// <summary>The package release number.</summary>
    internal const string Release = "1";

    /// <summary>Length of the RPM lead.</summary>
    private const int LeadLength = 96;

    /// <summary>Length of the package name field in the lead.</summary>
    private const int LeadNameLength = 66;

    /// <summary>Byte offset of the architecture number in the lead.</summary>
    private const int LeadArchitectureOffset = 8;

    /// <summary>Byte offset of the name in the lead.</summary>
    private const int LeadNameOffset = 10;

    /// <summary>Byte offset of the operating system number in the lead.</summary>
    private const int LeadOsOffset = 76;

    /// <summary>Byte offset of the signature type in the lead.</summary>
    private const int LeadSignatureTypeOffset = 78;

    /// <summary>The RPM lead magic number.</summary>
    private const uint LeadMagic = 0xEDABEEDB;

    /// <summary>The RPM major format version.</summary>
    private const byte LeadMajor = 3;

    /// <summary>The operating system number for Linux.</summary>
    private const ushort LeadLinux = 1;

    /// <summary>The lead architecture number for x86_64.</summary>
    private const ushort LeadArchitectureX64 = 1;

    /// <summary>The lead architecture number for aarch64.</summary>
    private const ushort LeadArchitectureArm64 = 19;

    /// <summary>The cpio link count of a directory.</summary>
    private const int CpioDirectoryLinks = 2;

    /// <summary>The cpio link count of a file or symbolic link.</summary>
    private const int CpioFileLinks = 1;

    /// <summary>The signature type for a header style signature.</summary>
    private const ushort HeaderSignature = 5;

    /// <summary>Signature header region tag.</summary>
    private const int RegionSignatures = 62;

    /// <summary>Main header region tag.</summary>
    private const int RegionImmutable = 63;

    /// <summary>Header sha256 digest signature tag.</summary>
    private const int SignatureSha256 = 273;

    /// <summary>Header plus payload size signature tag.</summary>
    private const int SignatureSize = 1000;

    /// <summary>Uncompressed payload size signature tag.</summary>
    private const int SignaturePayloadSize = 1007;

    /// <summary>The Name tag.</summary>
    private const int TagName = 1000;

    /// <summary>The Version tag.</summary>
    private const int TagVersion = 1001;

    /// <summary>The Release tag.</summary>
    private const int TagRelease = 1002;

    /// <summary>The Summary tag.</summary>
    private const int TagSummary = 1004;

    /// <summary>The Description tag.</summary>
    private const int TagDescription = 1005;

    /// <summary>The BuildTime tag.</summary>
    private const int TagBuildTime = 1006;

    /// <summary>The BuildHost tag.</summary>
    private const int TagBuildHost = 1007;

    /// <summary>The installed Size tag.</summary>
    private const int TagSize = 1009;

    /// <summary>The Vendor tag.</summary>
    private const int TagVendor = 1011;

    /// <summary>The License tag.</summary>
    private const int TagLicense = 1014;

    /// <summary>The Group tag.</summary>
    private const int TagGroup = 1016;

    /// <summary>The Url tag.</summary>
    private const int TagUrl = 1020;

    /// <summary>The Os tag.</summary>
    private const int TagOs = 1021;

    /// <summary>The Arch tag.</summary>
    private const int TagArch = 1022;

    /// <summary>The FileSizes tag.</summary>
    private const int TagFileSizes = 1028;

    /// <summary>The FileModes tag.</summary>
    private const int TagFileModes = 1030;

    /// <summary>The FileRdevs tag.</summary>
    private const int TagFileRdevs = 1033;

    /// <summary>The FileMtimes tag.</summary>
    private const int TagFileMtimes = 1034;

    /// <summary>The FileDigests tag.</summary>
    private const int TagFileDigests = 1035;

    /// <summary>The FileLinkTos tag.</summary>
    private const int TagFileLinkTos = 1036;

    /// <summary>The FileFlags tag.</summary>
    private const int TagFileFlags = 1037;

    /// <summary>The FileUserName tag.</summary>
    private const int TagFileUserName = 1039;

    /// <summary>The FileGroupName tag.</summary>
    private const int TagFileGroupName = 1040;

    /// <summary>The ProvideName tag.</summary>
    private const int TagProvideName = 1047;

    /// <summary>The RequireFlags tag.</summary>
    private const int TagRequireFlags = 1048;

    /// <summary>The RequireName tag.</summary>
    private const int TagRequireName = 1049;

    /// <summary>The RequireVersion tag.</summary>
    private const int TagRequireVersion = 1050;

    /// <summary>The FileDevices tag.</summary>
    private const int TagFileDevices = 1095;

    /// <summary>The FileInodes tag.</summary>
    private const int TagFileInodes = 1096;

    /// <summary>The FileLangs tag.</summary>
    private const int TagFileLangs = 1097;

    /// <summary>The ProvideFlags tag.</summary>
    private const int TagProvideFlags = 1112;

    /// <summary>The ProvideVersion tag.</summary>
    private const int TagProvideVersion = 1113;

    /// <summary>The DirIndexes tag.</summary>
    private const int TagDirIndexes = 1116;

    /// <summary>The BaseNames tag.</summary>
    private const int TagBaseNames = 1117;

    /// <summary>The DirNames tag.</summary>
    private const int TagDirNames = 1118;

    /// <summary>The PayloadFormat tag.</summary>
    private const int TagPayloadFormat = 1124;

    /// <summary>The PayloadCompressor tag.</summary>
    private const int TagPayloadCompressor = 1125;

    /// <summary>The PayloadFlags tag.</summary>
    private const int TagPayloadFlags = 1126;

    /// <summary>The header I18N table tag.</summary>
    private const int TagI18nTable = 100;

    /// <summary>The FileDigestAlgo tag.</summary>
    private const int TagFileDigestAlgo = 5011;

    /// <summary>The PayloadDigest tag; the SHA-256 of the compressed payload.</summary>
    private const int TagPayloadDigest = 5092;

    /// <summary>The PayloadDigestAlgo tag.</summary>
    private const int TagPayloadDigestAlgo = 5093;

    /// <summary>The PayloadDigestAlt tag; the SHA-256 of the uncompressed payload.</summary>
    private const int TagPayloadDigestAlt = 5097;

    /// <summary>The gzip quality of the payload; 9 is the maximum accepted by the encoder.</summary>
    private const int CompressionQuality = 9;

    /// <summary>The RecommendName tag.</summary>
    private const int TagRecommendName = 5046;

    /// <summary>The RecommendVersion tag.</summary>
    private const int TagRecommendVersion = 5047;

    /// <summary>The RecommendFlags tag.</summary>
    private const int TagRecommendFlags = 5048;

    /// <summary>The digest algorithm number for SHA-256.</summary>
    private const int DigestSha256 = 8;

    /// <summary>The dependency flag requiring an equal version.</summary>
    private const int SenseEqual = 8;

    /// <summary>The dependency flags of an rpmlib feature requirement.</summary>
    private const int SenseRpmLib = (1 << 24) | 2 | 8;

    /// <summary>The file flag marking a licence file.</summary>
    private const int FileLicense = 1 << 7;

    /// <summary>File type bits of a regular file.</summary>
    private const int ModeFile = 0x8000;

    /// <summary>File type bits of a directory.</summary>
    private const int ModeDirectory = 0x4000;

    /// <summary>File type bits of a symbolic link.</summary>
    private const int ModeSymlink = 0xA000;

    /// <summary>Permission bits of a symbolic link.</summary>
    private const int LinkPermissions = 0b1_1111_1111;

    /// <summary>Reported size of a directory.</summary>
    private const int DirectorySize = 4096;

    /// <summary>The size of a cpio header.</summary>
    private const int CpioHeaderLength = 110;

    /// <summary>The cpio archive end marker name.</summary>
    private const string CpioTrailer = "TRAILER!!!";

    /// <summary>The alignment of cpio records.</summary>
    private const int CpioAlignment = 4;

    /// <summary>The alignment of the signature header.</summary>
    private const int SignatureAlignment = 8;

    /// <summary>Builds an .rpm package.</summary>
    /// <param name="payload">The installed entries sorted by path.</param>
    /// <param name="version">The semantic version.</param>
    /// <param name="architecture">The RPM architecture such as x86_64.</param>
    /// <param name="output">The package to create.</param>
    internal static void Build(IReadOnlyList<PayloadEntry> payload, string version, string architecture, string output)
    {
        var rpmVersion = ToRpmVersion(version);
        var built = CreatePayload(payload);
        var header = CreateHeader(payload, rpmVersion, architecture, built);

        var signature = new HeaderBuilder();
        signature.AddString(SignatureSha256, Convert.ToHexStringLower(SHA256.HashData(header)));
        signature.AddInt32(SignatureSize, header.Length + built.Compressed.Length);
        signature.AddInt32(SignaturePayloadSize, (int)built.UncompressedLength);
        var signatureBytes = signature.Serialize(RegionSignatures);

        using var package = File.Create(output);
        package.Write(CreateLead($"{LinuxPayload.PackageName}-{rpmVersion}-{Release}", architecture));
        package.Write(signatureBytes);
        package.Write(new byte[(SignatureAlignment - (signatureBytes.Length % SignatureAlignment)) % SignatureAlignment]);
        package.Write(header);
        package.Write(built.Compressed);
    }

    /// <summary>Converts a semantic version to an RPM version.</summary>
    /// <param name="version">The semantic version.</param>
    /// <returns>The RPM version where pre-releases sort before the release.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToRpmVersion(string version) => version.Split('+')[0].Replace('-', '~');

    /// <summary>Creates the RPM lead.</summary>
    /// <param name="name">The package name, version and release.</param>
    /// <param name="architecture">The RPM architecture.</param>
    /// <returns>The 96 byte lead.</returns>
    private static byte[] CreateLead(string name, string architecture)
    {
        var lead = new byte[LeadLength];
        BinaryPrimitives.WriteUInt32BigEndian(lead, LeadMagic);
        lead[4] = LeadMajor;
        BinaryPrimitives.WriteUInt16BigEndian(lead.AsSpan(LeadArchitectureOffset), architecture == "x86_64" ? LeadArchitectureX64 : LeadArchitectureArm64);
        var nameBytes = Encoding.ASCII.GetBytes(name);
        nameBytes.AsSpan(0, Math.Min(nameBytes.Length, LeadNameLength - 1)).CopyTo(lead.AsSpan(LeadNameOffset));
        BinaryPrimitives.WriteUInt16BigEndian(lead.AsSpan(LeadOsOffset), LeadLinux);
        BinaryPrimitives.WriteUInt16BigEndian(lead.AsSpan(LeadSignatureTypeOffset), HeaderSignature);
        return lead;
    }

    /// <summary>Creates the gzip compressed cpio payload and its digests.</summary>
    /// <param name="payload">The installed entries.</param>
    /// <returns>The compressed payload, its uncompressed length and both SHA-256 digests.</returns>
    /// <exception cref="InvalidOperationException">The payload could not be compressed.</exception>
    private static PayloadResult CreatePayload(IReadOnlyList<PayloadEntry> payload)
    {
        using var buffer = new MemoryStream();
        for (var index = 0; index < payload.Count; index++)
        {
            _ = WriteCpioEntry(buffer, payload[index], index + 1);
        }

        _ = WriteCpioRecord(buffer, CpioTrailer, 0, 0, CpioFileLinks, 0, default);

        var uncompressed = buffer.ToArray();
        var destination = new byte[GZipEncoder.GetMaxCompressedLength(uncompressed.Length)];
        if (!GZipEncoder.TryCompress(uncompressed, destination, out var written, CompressionQuality))
        {
            throw new InvalidOperationException("The RPM payload could not be compressed.");
        }

        var compressed = destination.AsSpan(0, written).ToArray();
        return new(
            compressed,
            uncompressed.Length,
            Convert.ToHexStringLower(SHA256.HashData(compressed)),
            Convert.ToHexStringLower(SHA256.HashData(uncompressed)));
    }

    /// <summary>Writes one cpio entry with its content.</summary>
    /// <param name="output">The payload stream.</param>
    /// <param name="entry">The entry.</param>
    /// <param name="inode">The unique inode number.</param>
    /// <returns>The bytes written.</returns>
    /// <exception cref="InvalidDataException">The entry kind is unsupported.</exception>
    private static long WriteCpioEntry(Stream output, PayloadEntry entry, int inode)
    {
        var name = $".{entry.Path}";
        var mode = GetFileMode(entry);
        switch (entry.Kind)
        {
            case PayloadKind.Directory:
                return WriteCpioRecord(output, name, inode, mode, CpioDirectoryLinks, 0, default);
            case PayloadKind.Symlink:
            {
                var target = Encoding.UTF8.GetBytes(entry.Target!);
                return WriteCpioRecord(output, name, inode, mode, CpioFileLinks, target.Length, target);
            }

            case PayloadKind.File:
                using (var content = File.OpenRead(entry.Source!))
                {
                    var written = WriteCpioRecord(output, name, inode, mode, CpioFileLinks, content.Length, default);
                    content.CopyTo(output);
                    var padding = GetPadding(content.Length);
                    output.Write(new byte[padding]);
                    return written + content.Length + padding;
                }

            default:
                throw new InvalidDataException($"Unsupported entry kind {entry.Kind}.");
        }
    }

    /// <summary>Writes a cpio header, name and any inline content.</summary>
    /// <param name="output">The payload stream.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="inode">The inode number.</param>
    /// <param name="mode">The file type and permission bits.</param>
    /// <param name="links">The link count.</param>
    /// <param name="size">The content length.</param>
    /// <param name="inline">Content written here, or empty when the caller writes it.</param>
    /// <returns>The bytes written.</returns>
    private static long WriteCpioRecord(Stream output, string name, int inode, int mode, int links, long size, ReadOnlySpan<byte> inline)
    {
        var nameBytes = Encoding.UTF8.GetBytes(name);
        var time = LinuxPayload.BuildTime.ToUnixTimeSeconds();
        var header = $"070701{inode:X8}{mode:X8}{0:X8}{0:X8}{links:X8}{time:X8}{size:X8}{0:X8}{0:X8}{0:X8}{0:X8}{nameBytes.Length + 1:X8}{0:X8}";
        output.Write(Encoding.ASCII.GetBytes(header));
        output.Write(nameBytes);
        output.WriteByte(0);
        var namePadding = GetPadding(CpioHeaderLength + nameBytes.Length + 1);
        output.Write(new byte[namePadding]);
        long written = CpioHeaderLength + nameBytes.Length + 1 + namePadding;
        if (!inline.IsEmpty)
        {
            output.Write(inline);
            var padding = GetPadding(inline.Length);
            output.Write(new byte[padding]);
            written += inline.Length + padding;
        }

        return written;
    }

    /// <summary>Gets the padding that aligns a length to the cpio boundary.</summary>
    /// <param name="length">The length so far.</param>
    /// <returns>The padding byte count.</returns>
    private static int GetPadding(long length) => (int)((CpioAlignment - (length % CpioAlignment)) % CpioAlignment);

    /// <summary>Gets the file type and permission bits of an entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <returns>The mode bits.</returns>
    private static int GetFileMode(PayloadEntry entry) => entry.Kind switch
    {
        PayloadKind.Directory => ModeDirectory | entry.Mode,
        PayloadKind.Symlink => ModeSymlink | LinkPermissions,
        PayloadKind.File => ModeFile | entry.Mode,
        _ => entry.Mode,
    };

    /// <summary>Creates the main header.</summary>
    /// <param name="payload">The installed entries.</param>
    /// <param name="version">The RPM version.</param>
    /// <param name="architecture">The RPM architecture.</param>
    /// <param name="built">The built payload whose digests are recorded.</param>
    /// <returns>The serialised header.</returns>
    private static byte[] CreateHeader(IReadOnlyList<PayloadEntry> payload, string version, string architecture, in PayloadResult built)
    {
        var builder = new HeaderBuilder();
        var time = (int)LinuxPayload.BuildTime.ToUnixTimeSeconds();
        long installed = 0;
        foreach (var entry in payload)
        {
            installed += entry.GetLength();
        }

        builder.AddStringArray(TagI18nTable, ["C"]);
        builder.AddString(TagName, LinuxPayload.PackageName);
        builder.AddString(TagVersion, version);
        builder.AddString(TagRelease, Release);
        builder.AddI18nString(TagSummary, LinuxPayload.Summary);
        builder.AddI18nString(TagDescription, LinuxPayload.Description);
        builder.AddInt32(TagBuildTime, time);
        builder.AddString(TagBuildHost, "localhost");
        builder.AddInt32(TagSize, (int)installed);
        builder.AddString(TagVendor, LinuxPayload.Vendor);
        builder.AddString(TagLicense, LinuxPayload.License);
        builder.AddI18nString(TagGroup, "Unspecified");
        builder.AddString(TagUrl, LinuxPayload.Homepage);
        builder.AddString(TagOs, "linux");
        builder.AddString(TagArch, architecture);
        builder.AddString(TagPayloadFormat, "cpio");
        builder.AddString(TagPayloadCompressor, "gzip");
        builder.AddString(TagPayloadFlags, "9");
        builder.AddInt32(TagFileDigestAlgo, DigestSha256);
        builder.AddStringArray(TagPayloadDigest, [built.CompressedDigest]);
        builder.AddInt32(TagPayloadDigestAlgo, DigestSha256);
        builder.AddStringArray(TagPayloadDigestAlt, [built.UncompressedDigest]);
        AddFiles(builder, payload, time);
        AddDependencies(builder, version);
        return builder.Serialize(RegionImmutable);
    }

    /// <summary>Adds the per file tags.</summary>
    /// <param name="builder">The header.</param>
    /// <param name="payload">The installed entries.</param>
    /// <param name="time">The modification time.</param>
    private static void AddFiles(HeaderBuilder builder, IReadOnlyList<PayloadEntry> payload, int time)
    {
        var count = payload.Count;
        var sizes = new int[count];
        var modes = new int[count];
        var times = new int[count];
        var flags = new int[count];
        var devices = new int[count];
        var inodes = new int[count];
        var rdevs = new int[count];
        var digests = new string[count];
        var links = new string[count];
        var owners = new string[count];
        var languages = new string[count];
        var baseNames = new string[count];
        var directoryIndexes = new int[count];
        List<string> directoryNames = [];

        for (var index = 0; index < count; index++)
        {
            var entry = payload[index];
            var split = entry.Path.LastIndexOf('/') + 1;
            var directory = entry.Path[..split];
            var directoryIndex = directoryNames.IndexOf(directory);
            if (directoryIndex < 0)
            {
                directoryIndex = directoryNames.Count;
                directoryNames.Add(directory);
            }

            directoryIndexes[index] = directoryIndex;
            baseNames[index] = entry.Path[split..];
            modes[index] = GetFileMode(entry);
            sizes[index] = entry.Kind == PayloadKind.Directory ? DirectorySize : (int)entry.GetLength();
            times[index] = time;
            devices[index] = 1;
            inodes[index] = index + 1;
            rdevs[index] = 0;
            owners[index] = "root";
            languages[index] = string.Empty;
            links[index] = entry.Target ?? string.Empty;
            digests[index] = entry.Kind == PayloadKind.File ? Convert.ToHexStringLower(HashFile(entry.Source!)) : string.Empty;
            flags[index] = entry.Path.StartsWith("/usr/share/licenses/", StringComparison.Ordinal) && entry.Kind == PayloadKind.File ? FileLicense : 0;
        }

        builder.AddInt32(TagFileSizes, sizes);
        builder.AddInt16(TagFileModes, modes);
        builder.AddInt16(TagFileRdevs, rdevs);
        builder.AddInt32(TagFileMtimes, times);
        builder.AddStringArray(TagFileDigests, digests);
        builder.AddStringArray(TagFileLinkTos, links);
        builder.AddInt32(TagFileFlags, flags);
        builder.AddStringArray(TagFileUserName, owners);
        builder.AddStringArray(TagFileGroupName, owners);
        builder.AddInt32(TagFileDevices, devices);
        builder.AddInt32(TagFileInodes, inodes);
        builder.AddStringArray(TagFileLangs, languages);
        builder.AddInt32(TagDirIndexes, directoryIndexes);
        builder.AddStringArray(TagBaseNames, baseNames);
        builder.AddStringArray(TagDirNames, [.. directoryNames]);
    }

    /// <summary>Computes the SHA-256 of a file.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The hash.</returns>
    private static byte[] HashFile(string path)
    {
        using var stream = File.OpenRead(path);
        return SHA256.HashData(stream);
    }

    /// <summary>Adds provides, requires and recommends.</summary>
    /// <param name="builder">The header.</param>
    /// <param name="version">The RPM version.</param>
    private static void AddDependencies(HeaderBuilder builder, string version)
    {
        builder.AddStringArray(TagProvideName, [LinuxPayload.PackageName]);
        builder.AddInt32(TagProvideFlags, SenseEqual);
        builder.AddStringArray(TagProvideVersion, [$"{version}-{Release}"]);

        List<string> names = ["rpmlib(PayloadFilesHavePrefix)", "rpmlib(CompressedFileNames)", "rpmlib(FileDigests)"];
        List<string> versions = ["4.0-1", "3.0.4-1", "4.6.0-1"];
        List<int> flags = [SenseRpmLib, SenseRpmLib, SenseRpmLib];
        foreach (var name in LinuxPayload.RpmDepends)
        {
            names.Add(name);
            versions.Add(string.Empty);
            flags.Add(0);
        }

        builder.AddInt32(TagRequireFlags, [.. flags]);
        builder.AddStringArray(TagRequireName, [.. names]);
        builder.AddStringArray(TagRequireVersion, [.. versions]);

        var recommended = LinuxPayload.RpmRecommends;
        builder.AddStringArray(TagRecommendName, recommended);
        builder.AddInt32(TagRecommendFlags, new int[recommended.Length]);
        var recommendedVersions = new string[recommended.Length];
        Array.Fill(recommendedVersions, string.Empty);
        builder.AddStringArray(TagRecommendVersion, recommendedVersions);
    }

    /// <summary>The built payload and what the header records about it.</summary>
    /// <param name="Compressed">The gzip compressed cpio archive.</param>
    /// <param name="UncompressedLength">The length of the cpio archive before compression.</param>
    /// <param name="CompressedDigest">The lower case hex SHA-256 of the compressed archive.</param>
    /// <param name="UncompressedDigest">The lower case hex SHA-256 of the cpio archive.</param>
    private readonly record struct PayloadResult(byte[] Compressed, long UncompressedLength, string CompressedDigest, string UncompressedDigest);

    /// <summary>Builds and serialises an RPM header structure.</summary>
    private sealed class HeaderBuilder
    {
        /// <summary>Header entry type for 16-bit integers.</summary>
        private const int TypeInt16 = 3;

        /// <summary>Header entry type for 32-bit integers.</summary>
        private const int TypeInt32 = 4;

        /// <summary>Header entry type for a string.</summary>
        private const int TypeString = 6;

        /// <summary>Header entry type for binary data.</summary>
        private const int TypeBinary = 7;

        /// <summary>Header entry type for a string array.</summary>
        private const int TypeStringArray = 8;

        /// <summary>Header entry type for an internationalised string.</summary>
        private const int TypeI18nString = 9;

        /// <summary>Size of one header index entry.</summary>
        private const int IndexEntryLength = 16;

        /// <summary>The entries ordered by tag.</summary>
        private readonly SortedDictionary<int, Entry> _entries = [];

        /// <summary>Gets the RPM header magic bytes and version.</summary>
        private static ReadOnlySpan<byte> HeaderMagic => [0x8E, 0xAD, 0xE8, 0x01, 0x00, 0x00, 0x00, 0x00];

        /// <summary>Adds a string entry.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="value">The value.</param>
        internal void AddString(int tag, string value) => _entries[tag] = new(TypeString, 1, Encoding.UTF8.GetBytes($"{value}\u0000"));

        /// <summary>Adds an internationalised string entry.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="value">The value.</param>
        internal void AddI18nString(int tag, string value) => _entries[tag] = new(TypeI18nString, 1, Encoding.UTF8.GetBytes($"{value}\u0000"));

        /// <summary>Adds a string array entry.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="values">The values.</param>
        internal void AddStringArray(int tag, string[] values)
        {
            using var data = new MemoryStream();
            foreach (var value in values)
            {
                data.Write(Encoding.UTF8.GetBytes(value));
                data.WriteByte(0);
            }

            _entries[tag] = new(TypeStringArray, values.Length, data.ToArray());
        }

        /// <summary>Adds a 32-bit integer array entry.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="values">The values.</param>
        internal void AddInt32(int tag, params int[] values)
        {
            var data = new byte[values.Length * sizeof(int)];
            for (var index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteInt32BigEndian(data.AsSpan(index * sizeof(int)), values[index]);
            }

            _entries[tag] = new(TypeInt32, values.Length, data);
        }

        /// <summary>Adds a 16-bit integer array entry.</summary>
        /// <param name="tag">The tag.</param>
        /// <param name="values">The values.</param>
        internal void AddInt16(int tag, int[] values)
        {
            var data = new byte[values.Length * sizeof(short)];
            for (var index = 0; index < values.Length; index++)
            {
                BinaryPrimitives.WriteUInt16BigEndian(data.AsSpan(index * sizeof(short)), (ushort)values[index]);
            }

            _entries[tag] = new(TypeInt16, values.Length, data);
        }

        /// <summary>Serialises the header with a region tag.</summary>
        /// <param name="regionTag">The region tag that covers all entries.</param>
        /// <returns>The header bytes.</returns>
        internal byte[] Serialize(int regionTag)
        {
            using var store = new MemoryStream();
            var index = new List<(int Tag, int Type, int Offset, int Count)>();
            foreach (var (tag, entry) in _entries)
            {
                var alignment = GetAlignment(entry.Type);
                while (store.Length % alignment != 0)
                {
                    store.WriteByte(0);
                }

                index.Add((tag, entry.Type, (int)store.Length, entry.Count));
                store.Write(entry.Data);
            }

            var regionOffset = (int)store.Length;
            var total = index.Count + 1;
            WriteIndexEntry(store, regionTag, TypeBinary, -(total * IndexEntryLength), IndexEntryLength);

            using var output = new MemoryStream();
            output.Write(HeaderMagic);
            WriteInt32(output, total);
            WriteInt32(output, (int)store.Length);
            WriteIndexEntry(output, regionTag, TypeBinary, regionOffset, IndexEntryLength);
            foreach (var (tag, type, offset, count) in index)
            {
                WriteIndexEntry(output, tag, type, offset, count);
            }

            store.Position = 0;
            store.CopyTo(output);
            return output.ToArray();
        }

        /// <summary>Gets the data alignment of an entry type.</summary>
        /// <param name="type">The entry type.</param>
        /// <returns>The alignment in bytes.</returns>
        private static int GetAlignment(int type) => type switch
        {
            TypeInt16 => sizeof(short),
            TypeInt32 => sizeof(int),
            _ => 1,
        };

        /// <summary>Writes a big endian 32-bit integer.</summary>
        /// <param name="output">The destination.</param>
        /// <param name="value">The value.</param>
        private static void WriteInt32(Stream output, int value)
        {
            Span<byte> bytes = stackalloc byte[sizeof(int)];
            BinaryPrimitives.WriteInt32BigEndian(bytes, value);
            output.Write(bytes);
        }

        /// <summary>Writes one 16 byte index entry.</summary>
        /// <param name="output">The destination.</param>
        /// <param name="tag">The tag.</param>
        /// <param name="type">The type.</param>
        /// <param name="offset">The data offset.</param>
        /// <param name="count">The value count.</param>
        private static void WriteIndexEntry(Stream output, int tag, int type, int offset, int count)
        {
            WriteInt32(output, tag);
            WriteInt32(output, type);
            WriteInt32(output, offset);
            WriteInt32(output, count);
        }

        /// <summary>One header entry.</summary>
        /// <param name="Type">The entry type.</param>
        /// <param name="Count">The number of values.</param>
        /// <param name="Data">The encoded values.</param>
        private readonly record struct Entry(int Type, int Count, byte[] Data);
    }
}
