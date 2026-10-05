// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Tar;
using System.Globalization;
using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Builds Debian packages with .NET only.</summary>
internal static class DebBuilder
{
    /// <summary>The alignment of ar members; odd sized members are padded to it.</summary>
    private const int ArAlignment = 2;

    /// <summary>Width of the ar member name field.</summary>
    private const int ArNameWidth = 16;

    /// <summary>Width of the ar modification time field.</summary>
    private const int ArTimeWidth = 12;

    /// <summary>Width of the ar owner fields.</summary>
    private const int ArOwnerWidth = 6;

    /// <summary>Width of the ar mode field.</summary>
    private const int ArModeWidth = 8;

    /// <summary>Width of the ar size field.</summary>
    private const int ArSizeWidth = 10;

    /// <summary>Total size of an ar member header.</summary>
    private const int ArHeaderLength = 60;

    /// <summary>Bytes in a kibibyte.</summary>
    private const int Kibibyte = 1024;

    /// <summary>Permission bits for a directory (rwxr-xr-x).</summary>
    private const int DirectoryMode = 0b1_1110_1101;

    /// <summary>Permission bits for a control file (rw-r--r--).</summary>
    private const int ControlMode = 0b1_1010_0100;

    /// <summary>Gets the ar global header.</summary>
    private static ReadOnlySpan<byte> ArMagic => "!<arch>\n"u8;

    /// <summary>Builds a .deb package.</summary>
    /// <param name="payload">The installed entries.</param>
    /// <param name="version">The semantic version.</param>
    /// <param name="architecture">The Debian architecture such as amd64.</param>
    /// <param name="output">The package to create.</param>
    internal static void Build(IReadOnlyList<PayloadEntry> payload, string version, string architecture, string output)
    {
        var entries = WithParentDirectories(payload);
        var control = BuildControl(entries, ToDebianVersion(version), architecture);
        var controlArchive = CreateControlArchive(control);
        var dataArchive = CreateDataArchive(entries);

        using var package = File.Create(output);
        package.Write(ArMagic);
        WriteMember(package, "debian-binary", "2.0\n"u8.ToArray());
        WriteMember(package, "control.tar.gz", controlArchive);
        WriteMember(package, "data.tar.gz", dataArchive);
    }

    /// <summary>Converts a semantic version to a Debian version.</summary>
    /// <param name="version">The semantic version.</param>
    /// <returns>The Debian version where pre-releases sort before the release.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToDebianVersion(string version) => version.Split('+')[0].Replace('-', '~');

    /// <summary>Adds every missing parent directory.</summary>
    /// <param name="payload">The installed entries.</param>
    /// <returns>All entries sorted by path with parents first.</returns>
    private static SortedDictionary<string, PayloadEntry> WithParentDirectories(IReadOnlyList<PayloadEntry> payload)
    {
        SortedDictionary<string, PayloadEntry> entries = new(StringComparer.Ordinal);
        foreach (var entry in payload)
        {
            entries[entry.Path] = entry;
        }

        foreach (var entry in payload)
        {
            var end = entry.Path.LastIndexOf('/');
            while (end > 0)
            {
                var parent = entry.Path[..end];
                _ = entries.TryAdd(parent, new(parent, PayloadKind.Directory, DirectoryMode, null, null));
                end = parent.LastIndexOf('/');
            }
        }

        return entries;
    }

    /// <summary>Creates the control file text.</summary>
    /// <param name="entries">All installed entries.</param>
    /// <param name="version">The Debian version.</param>
    /// <param name="architecture">The Debian architecture.</param>
    /// <returns>The control file content.</returns>
    private static string BuildControl(SortedDictionary<string, PayloadEntry> entries, string version, string architecture)
    {
        long size = 0;
        foreach (var entry in entries.Values)
        {
            size += entry.GetLength();
        }

        var text = new StringBuilder();
        _ = text.Append("Package: ").Append(LinuxPayload.PackageName).Append('\n');
        _ = text.Append("Version: ").Append(version).Append('\n');
        _ = text.Append("Architecture: ").Append(architecture).Append('\n');
        _ = text.Append("Maintainer: ").Append(LinuxPayload.Vendor).Append('\n');
        _ = text.Append("Installed-Size: ").Append((size + Kibibyte - 1) / Kibibyte).Append('\n');
        _ = text.Append("Depends: ").AppendJoin(", ", LinuxPayload.DebianDepends).Append('\n');
        _ = text.Append("Recommends: ").AppendJoin(", ", LinuxPayload.DebianRecommends).Append('\n');
        _ = text.Append("Section: graphics\nPriority: optional\n");
        _ = text.Append("Homepage: ").Append(LinuxPayload.Homepage).Append('\n');
        _ = text.Append("Description: ").Append(LinuxPayload.Summary).Append('\n');
        foreach (var line in LinuxPayload.Description.Split('\n'))
        {
            _ = text.Append(' ').Append(line.Length == 0 ? "." : line).Append('\n');
        }

        return text.ToString();
    }

    /// <summary>Creates the compressed control archive.</summary>
    /// <param name="control">The control file content.</param>
    /// <returns>The gzip compressed tar bytes.</returns>
    private static byte[] CreateControlArchive(string control)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Ustar, true))
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(control));
            var entry = new UstarTarEntry(TarEntryType.RegularFile, "./control")
            {
                Mode = (UnixFileMode)ControlMode,
                UserName = "root",
                GroupName = "root",
                ModificationTime = LinuxPayload.BuildTime,
                DataStream = content,
            };
            writer.WriteEntry(entry);
        }

        return buffer.ToArray();
    }

    /// <summary>Creates the compressed data archive.</summary>
    /// <param name="entries">All installed entries.</param>
    /// <returns>The gzip compressed tar bytes.</returns>
    private static byte[] CreateDataArchive(SortedDictionary<string, PayloadEntry> entries)
    {
        using var buffer = new MemoryStream();
        using (var gzip = new GZipStream(buffer, CompressionLevel.SmallestSize, true))
        using (var writer = new TarWriter(gzip, TarEntryFormat.Ustar, true))
        {
            writer.WriteEntry(CreateEntry(TarEntryType.Directory, "./", DirectoryMode));
            foreach (var entry in entries.Values)
            {
                WriteEntry(writer, entry);
            }
        }

        return buffer.ToArray();
    }

    /// <summary>Writes one installed entry to the data archive.</summary>
    /// <param name="writer">The tar writer.</param>
    /// <param name="entry">The entry.</param>
    /// <exception cref="InvalidDataException">The entry kind is unsupported.</exception>
    private static void WriteEntry(TarWriter writer, PayloadEntry entry)
    {
        var name = $".{entry.Path}";
        switch (entry.Kind)
        {
            case PayloadKind.Directory:
            {
                writer.WriteEntry(CreateEntry(TarEntryType.Directory, $"{name}/", entry.Mode));
                break;
            }

            case PayloadKind.Symlink:
            {
                var link = CreateEntry(TarEntryType.SymbolicLink, name, entry.Mode);
                link.LinkName = entry.Target!;
                writer.WriteEntry(link);
                break;
            }

            case PayloadKind.File:
            {
                using (var content = File.OpenRead(entry.Source!))
                {
                    var file = CreateEntry(TarEntryType.RegularFile, name, entry.Mode);
                    file.DataStream = content;
                    writer.WriteEntry(file);
                }

                break;
            }

            default:
                throw new InvalidDataException($"Unsupported entry kind {entry.Kind}.");
        }
    }

    /// <summary>Creates a root owned tar entry.</summary>
    /// <param name="type">The entry type.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="mode">The permission bits.</param>
    /// <returns>The entry.</returns>
    private static UstarTarEntry CreateEntry(TarEntryType type, string name, int mode) => new(type, name)
    {
        Mode = (UnixFileMode)mode,
        UserName = "root",
        GroupName = "root",
        ModificationTime = LinuxPayload.BuildTime,
    };

    /// <summary>Writes one ar member.</summary>
    /// <param name="output">The package stream.</param>
    /// <param name="name">The member name.</param>
    /// <param name="content">The member content.</param>
    /// <exception cref="InvalidDataException">The member header is not 60 bytes.</exception>
    private static void WriteMember(Stream output, string name, byte[] content)
    {
        var modified = LinuxPayload.BuildTime.ToUnixTimeSeconds().ToString(CultureInfo.InvariantCulture);
        var size = content.Length.ToString(CultureInfo.InvariantCulture);
        var header = Encoding.ASCII.GetBytes(
            string.Concat(
                name.PadRight(ArNameWidth),
                modified.PadRight(ArTimeWidth),
                "0".PadRight(ArOwnerWidth),
                "0".PadRight(ArOwnerWidth),
                "100644".PadRight(ArModeWidth),
                size.PadRight(ArSizeWidth),
                "`\n"));
        if (header.Length != ArHeaderLength)
        {
            throw new InvalidDataException("The ar member header has the wrong length.");
        }

        output.Write(header);
        output.Write(content);
        if (content.Length % ArAlignment != 0)
        {
            output.WriteByte((byte)'\n');
        }
    }
}
