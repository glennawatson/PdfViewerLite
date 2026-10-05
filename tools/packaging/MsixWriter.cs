// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Security.Cryptography;
using System.Xml.Linq;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Repackages MSIX payloads with independently compressed hash blocks.</summary>
internal static class MsixWriter
{
    /// <summary>Package block map entry name.</summary>
    private const string BlockMapName = "AppxBlockMap.xml";

    /// <summary>Writes an unsigned MSIX from the staged application files.</summary>
    /// <param name="folder">The staging directory.</param>
    /// <param name="package">The output package.</param>
    internal static void Build(string folder, string package)
    {
        XNamespace ns = "http://schemas.microsoft.com/appx/2010/blockmap";

        // The native verifier reads HashMethod through the end of the opening tag.
        var map = new XDocument(new XElement(ns + "BlockMap", new XAttribute("xmlns", ns.NamespaceName), new XAttribute("HashMethod", "http://www.w3.org/2001/04/xmlenc#sha256")));
        using var destination = File.Create(package);
        using var writer = new MsixZipWriter(destination);
        var paths = Directory.GetFiles(folder, "*", SearchOption.AllDirectories);
        Array.Sort(paths, StringComparer.Ordinal);
        foreach (var path in paths)
        {
            var name = Path.GetRelativePath(folder, path).Replace('\\', '/');
            var file = new XElement(ns + "File", new XAttribute("Name", name.Replace('/', '\\')));
            map.Root!.Add(file);
            using var source = File.OpenRead(path);
            writer.Write(name, File.GetLastWriteTime(path), source, source.Length, file);
        }

        WriteXml(writer, BlockMapName, map);
        WriteXml(writer, "[Content_Types].xml", CreateContentTypes(paths, folder));
    }

    /// <summary>Replaces executable payloads and regenerates the compressed block map.</summary>
    /// <param name="asset">The MSIX package path.</param>
    /// <param name="payloads">Original hashes mapped to signed executable paths.</param>
    /// <exception cref="InvalidDataException">The package has no block map or a payload has no signed replacement.</exception>
    internal static void Replace(string asset, Dictionary<string, string> payloads)
    {
        var temporary = $"{asset}.repacked";
        using (var archive = ZipFile.OpenRead(asset))
        {
            var entry = archive.GetEntry(BlockMapName) ?? throw new InvalidDataException("The MSIX package has no block map.");
            using var mapStream = entry.Open();
            var map = XDocument.Load(mapStream);
            var files = new Dictionary<string, XElement>(StringComparer.Ordinal);
            foreach (var file in map.Root!.Elements(map.Root.Name.Namespace + "File"))
            {
                files.Add(file.Attribute("Name")!.Value.Replace('\\', '/'), file);
            }

            using var destination = File.Create(temporary);
            using var writer = new MsixZipWriter(destination);
            foreach (var item in archive.Entries)
            {
                if (item.FullName is BlockMapName or "AppxSignature.p7x")
                {
                    continue;
                }

                _ = files.TryGetValue(item.FullName, out var file);
                WriteEntry(writer, item, file, payloads);
            }

            WriteXml(writer, BlockMapName, map);
        }

        File.Move(temporary, asset, true);
    }

    /// <summary>Creates explicit MIME types for package payloads and metadata.</summary>
    /// <param name="paths">The staged file paths.</param>
    /// <param name="folder">The staging directory.</param>
    /// <returns>The package content type document.</returns>
    private static XDocument CreateContentTypes(string[] paths, string folder)
    {
        XNamespace ns = "http://schemas.openxmlformats.org/package/2006/content-types";
        var types = new XElement(ns + "Types");
        foreach (var path in paths)
        {
            var name = Path.GetRelativePath(folder, path).Replace('\\', '/');
            var contentType = name == "AppxManifest.xml" ? "application/vnd.ms-appx.manifest+xml" : Path.GetExtension(name).ToLowerInvariant() switch
            {
                ".png" => "image/png",
                ".exe" or ".dll" => "application/x-msdownload",
                _ => "application/octet-stream",
            };
            types.Add(new XElement(ns + "Override", new XAttribute("PartName", $"/{name}"), new XAttribute("ContentType", contentType)));
        }

        types.Add(
            new XElement(ns + "Override", new XAttribute("PartName", $"/{BlockMapName}"), new XAttribute("ContentType", "application/vnd.ms-appx.blockmap+xml")));
        return new(types);
    }

    /// <summary>Writes XML metadata into the package.</summary>
    /// <param name="writer">The ZIP writer.</param>
    /// <param name="name">The entry name.</param>
    /// <param name="document">The metadata document.</param>
    private static void WriteXml(MsixZipWriter writer, string name, XDocument document)
    {
        using var source = new MemoryStream();
        document.Save(source);
        source.Position = 0;
        writer.Write(name, DateTime.UnixEpoch, source, source.Length, null);
    }

    /// <summary>Writes the original payload or its signed replacement.</summary>
    /// <param name="writer">The package writer.</param>
    /// <param name="entry">The original archive entry.</param>
    /// <param name="file">The block map element.</param>
    /// <param name="payloads">Original hashes mapped to signed executables.</param>
    private static void WriteEntry(MsixZipWriter writer, ZipArchiveEntry entry, XElement? file, Dictionary<string, string> payloads)
    {
        var signed = FindReplacement(entry, payloads);
        using var source = signed is null ? entry.Open() : File.OpenRead(signed);
        writer.Write(entry.FullName, entry.LastWriteTime.DateTime, source, signed is null ? entry.Length : source.Length, file);
    }

    /// <summary>Finds the signed replacement for an executable entry.</summary>
    /// <param name="entry">The original archive entry.</param>
    /// <param name="payloads">Original hashes mapped to signed executable paths.</param>
    /// <returns>The signed path, or null for a non-executable entry.</returns>
    /// <exception cref="InvalidDataException">The executable has no signed replacement.</exception>
    private static string? FindReplacement(ZipArchiveEntry entry, Dictionary<string, string> payloads)
    {
        if (!entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
        {
            return null;
        }

        using var source = entry.Open();
        var hash = Convert.ToHexString(SHA256.HashData(source));
        return payloads.TryGetValue(hash, out var signed) ? signed : throw new InvalidDataException($"No signed payload matches {entry.FullName}.");
    }
}
