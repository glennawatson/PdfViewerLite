// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Updates Windows release packages with signed executable payloads.</summary>
internal static class WindowsPayload
{
    /// <summary>MSIX hash block length.</summary>
    private const int BlockLength = 65_536;

    /// <summary>MSIX block map entry name.</summary>
    private const string BlockMapName = "AppxBlockMap.xml";

    /// <summary>ZIP local file header length before the filename.</summary>
    private const int LocalHeaderLength = 30;

    /// <summary>Header row count in an exported MSI table.</summary>
    private const int TableHeaderRows = 3;

    /// <summary>File identifier column in the MSI File table.</summary>
    private const int FileKeyColumn = 0;

    /// <summary>File size column in the MSI File table.</summary>
    private const int FileSizeColumn = 3;

    /// <summary>File name column in the MSI File table.</summary>
    private const int FileNameColumn = 2;

    /// <summary>Cabinet sequence column in the MSI File table.</summary>
    private const int SequenceColumn = 7;

    /// <summary>Extracts executable payloads from Windows portable archives.</summary>
    /// <param name="assets">Release package paths.</param>
    /// <param name="scratch">Temporary signing directory.</param>
    /// <returns>Original SHA-256 hashes mapped to payload paths.</returns>
    /// <exception cref="InvalidDataException">The archives contain no executable payloads.</exception>
    internal static Dictionary<string, string> Extract(string[] assets, string scratch)
    {
        var folder = Path.Combine(scratch, "pe");
        _ = Directory.CreateDirectory(folder);
        var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var asset in assets)
        {
            if (!asset.Contains("-win-", StringComparison.Ordinal) || !asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            ExtractArchive(asset, folder, payloads);
        }

        if (payloads.Count == 0)
        {
            throw new InvalidDataException("No Windows executable payloads were downloaded.");
        }

        return payloads;
    }

    /// <summary>Places signed payloads into each Windows release package.</summary>
    /// <param name="assets">Release package paths.</param>
    /// <param name="payloads">Original hashes mapped to signed payload paths.</param>
    /// <param name="scratch">Temporary signing directory.</param>
    internal static void Replace(string[] assets, Dictionary<string, string> payloads, string scratch)
    {
        foreach (var asset in assets)
        {
            if (asset.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceMsi(asset, payloads, scratch);
            }
            else if (asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)
                || (asset.Contains("-win-", StringComparison.Ordinal) && asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase)))
            {
                ReplaceZip(asset, payloads);
            }
        }
    }

    /// <summary>Extracts distinct executable bytes to filenames derived from their hashes.</summary>
    /// <param name="asset">The portable archive.</param>
    /// <param name="folder">The extraction directory.</param>
    /// <param name="payloads">Original hashes mapped to payload paths.</param>
    private static void ExtractArchive(string asset, string folder, Dictionary<string, string> payloads)
    {
        using var archive = ZipFile.OpenRead(asset);
        foreach (var entry in archive.Entries)
        {
            if (!IsExecutable(entry.FullName))
            {
                continue;
            }

            using var source = entry.Open();
            var hash = Convert.ToHexString(SHA256.HashData(source));
            if (payloads.ContainsKey(hash))
            {
                continue;
            }

            var extension = entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) ? ".exe" : ".dll";
            var target = Path.Combine(folder, $"{hash}{extension}");
            entry.ExtractToFile(target);
            payloads.Add(hash, target);
        }
    }

    /// <summary>Replaces executable entries and updates the MSIX block map.</summary>
    /// <param name="asset">The archive.</param>
    /// <param name="payloads">Original hashes mapped to signed payload paths.</param>
    /// <exception cref="InvalidDataException">An executable has no matching signed payload.</exception>
    private static void ReplaceZip(string asset, Dictionary<string, string> payloads)
    {
        using var archive = ZipFile.Open(asset, ZipArchiveMode.Update);
        XDocument? blockMap = null;
        if (archive.GetEntry(BlockMapName) is { } mapEntry)
        {
            using var source = mapEntry.Open();
            blockMap = XDocument.Load(source);
            archive.GetEntry("AppxSignature.p7x")?.Delete();
        }

        ZipArchiveEntry[] entries = [.. archive.Entries];
        foreach (var entry in entries)
        {
            if (!IsExecutable(entry.FullName))
            {
                continue;
            }

            string hash;
            using (var source = entry.Open())
            {
                hash = Convert.ToHexString(SHA256.HashData(source));
            }

            if (!payloads.TryGetValue(hash, out var signed))
            {
                throw new InvalidDataException($"No signed payload matches {entry.FullName} in {asset}.");
            }

            var name = entry.FullName;
            var attributes = entry.ExternalAttributes;
            var modified = entry.LastWriteTime;
            entry.Delete();
            var replacement = archive.CreateEntry(name, blockMap is null ? CompressionLevel.Optimal : CompressionLevel.NoCompression);
            replacement.ExternalAttributes = attributes;
            replacement.LastWriteTime = modified;
            using (var source = File.OpenRead(signed))
            using (var target = replacement.Open())
            {
                source.CopyTo(target);
            }

            if (blockMap is not null)
            {
                UpdateBlockMap(blockMap, name, signed);
            }
        }

        if (blockMap is not null)
        {
            archive.GetEntry(BlockMapName)!.Delete();
            using var target = archive.CreateEntry(BlockMapName, CompressionLevel.NoCompression).Open();
            blockMap.Save(target);
        }
    }

    /// <summary>Hashes stored MSIX entries in uncompressed 64 KiB blocks.</summary>
    /// <param name="map">The package block map.</param>
    /// <param name="name">Archive entry name.</param>
    /// <param name="signed">Signed payload path.</param>
    /// <exception cref="InvalidDataException">The entry is missing from the block map.</exception>
    private static void UpdateBlockMap(XDocument map, string name, string signed)
    {
        var ns = map.Root!.Name.Namespace;
        foreach (var file in map.Root.Elements(ns + "File"))
        {
            if (((string?)file.Attribute("Name"))?.Replace('\\', '/') != name)
            {
                continue;
            }

            file.SetAttributeValue("Size", new FileInfo(signed).Length);
            file.SetAttributeValue("LfhSize", LocalHeaderLength + Encoding.UTF8.GetByteCount(name));
            file.RemoveNodes();
            using var source = File.OpenRead(signed);
            var buffer = new byte[BlockLength];
            int count;
            while ((count = source.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)) > 0)
            {
                file.Add(new XElement(ns + "Block", new XAttribute("Hash", Convert.ToBase64String(SHA256.HashData(buffer.AsSpan(0, count))))));
            }

            return;
        }

        throw new InvalidDataException($"The MSIX block map does not contain {name}.");
    }

    /// <summary>Updates the embedded cabinet and file sizes without changing installer tables.</summary>
    /// <param name="asset">The MSI.</param>
    /// <param name="payloads">Original hashes mapped to signed payload paths.</param>
    /// <param name="scratch">Temporary signing directory.</param>
    /// <exception cref="InvalidOperationException">The MSI extraction command could not start.</exception>
    /// <exception cref="InvalidDataException">The MSI cabinet could not be extracted or rebuilt.</exception>
    private static void ReplaceMsi(string asset, Dictionary<string, string> payloads, string scratch)
    {
        var folder = Path.Combine(scratch, Path.GetFileName(asset));
        _ = Directory.CreateDirectory(folder);
        var cabinet = Path.Combine(folder, "original.cab");
        ExtractCabinet(asset, cabinet);
        var extracted = Path.Combine(folder, "cabinet");
        _ = Directory.CreateDirectory(extracted);
        BuildTools.Run("cabextract", "-q", "-d", extracted, cabinet);
        Console.WriteLine($"[command]msiinfo export {asset} File");
        var table = Process.RunAndCaptureText("msiinfo", ["export", asset, "File"]);
        if (table.ExitStatus.ExitCode != 0)
        {
            throw new InvalidDataException(table.StandardError);
        }

        var rows = table.StandardOutput.Replace("\r", string.Empty, StringComparison.Ordinal).TrimEnd('\n').Split('\n');
        var files = new List<(string Name, int Sequence)>();
        for (var index = TableHeaderRows; index < rows.Length; index++)
        {
            var columns = rows[index].Split('\t');
            var path = Path.Combine(extracted, columns[FileKeyColumn]);
            string hash;
            using (var source = File.OpenRead(path))
            {
                hash = Convert.ToHexString(SHA256.HashData(source));
            }

            if (payloads.TryGetValue(hash, out var signed))
            {
                File.Copy(signed, path, true);
                columns[FileSizeColumn] = new FileInfo(path).Length.ToString(CultureInfo.InvariantCulture);
                rows[index] = string.Join('\t', columns);
            }
            else if (IsExecutable(columns[FileNameColumn]))
            {
                throw new InvalidDataException($"No signed payload matches {columns[FileNameColumn]} in {asset}.");
            }

            files.Add((columns[FileKeyColumn], int.Parse(columns[SequenceColumn], CultureInfo.InvariantCulture)));
        }

        files.Sort(static (left, right) => left.Sequence.CompareTo(right.Sequence));
        var updatedCabinet = Path.Combine(folder, "signed.cab");
        var arguments = new List<string> { "-c", "-z", updatedCabinet };
        foreach (var file in files)
        {
            arguments.Add(file.Name);
        }

        Console.WriteLine($"[command]gcab {string.Join(' ', arguments)}");
        if (Process.Run(new ProcessStartInfo("gcab", arguments) { WorkingDirectory = extracted }).ExitCode != 0)
        {
            throw new InvalidDataException("Could not rebuild the MSI cabinet.");
        }

        var idt = Path.Combine(folder, "File.idt");
        File.WriteAllText(idt, $"{string.Join("\r\n", rows)}\r\n");
        BuildTools.Run("msibuild", asset, "-a", "app.cab", updatedCabinet, "-i", idt);
    }

    /// <summary>Copies the embedded MSI cabinet stream without logging binary output.</summary>
    /// <param name="asset">The MSI.</param>
    /// <param name="cabinet">The extracted cabinet path.</param>
    /// <exception cref="InvalidOperationException">The extraction command could not start.</exception>
    /// <exception cref="InvalidDataException">The cabinet could not be extracted.</exception>
    private static void ExtractCabinet(string asset, string cabinet)
    {
        Console.WriteLine($"[command]msiinfo extract {asset} app.cab");
        var start = new ProcessStartInfo("msiinfo", ["extract", asset, "app.cab"]) { RedirectStandardOutput = true };
        using var process = Process.Start(start) ?? throw new InvalidOperationException("Could not start msiinfo.");
        using var target = File.Create(cabinet);
        process.StandardOutput.BaseStream.CopyTo(target);
        if (process.WaitForExitStatus().ExitCode != 0)
        {
            throw new InvalidDataException("Could not extract the MSI cabinet.");
        }
    }

    /// <summary>Identifies executable archive entries.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>Whether the entry is an EXE or DLL.</returns>
    private static bool IsExecutable(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
}
