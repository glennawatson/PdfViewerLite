// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.IO.Compression;
using System.Security.Cryptography;

using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Updates Windows release packages with signed executable payloads.</summary>
internal static class WindowsPayload
{
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
    /// <param name="packer">Microsoft MSIX packer path.</param>
    internal static void Replace(string[] assets, Dictionary<string, string> payloads, string scratch, string packer)
    {
        foreach (var asset in assets)
        {
            if (asset.EndsWith(".msi", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceMsi(asset, payloads, scratch);
            }
            else if (asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
            {
                ReplaceMsix(asset, payloads, scratch, packer);
            }
            else if (asset.Contains("-win-", StringComparison.Ordinal) && asset.EndsWith(".zip", StringComparison.OrdinalIgnoreCase))
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

    /// <summary>Replaces executable entries in a portable archive.</summary>
    /// <param name="asset">The archive.</param>
    /// <param name="payloads">Original hashes mapped to signed payload paths.</param>
    /// <exception cref="InvalidDataException">An executable has no matching signed payload.</exception>
    private static void ReplaceZip(string asset, Dictionary<string, string> payloads)
    {
        using var archive = ZipFile.Open(asset, ZipArchiveMode.Update);
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
            var replacement = archive.CreateEntry(name, CompressionLevel.Optimal);
            replacement.ExternalAttributes = attributes;
            replacement.LastWriteTime = modified;
            using (var source = File.OpenRead(signed))
            using (var target = replacement.Open())
            {
                source.CopyTo(target);
            }
        }
    }

    /// <summary>Repacks signed MSIX payloads with Microsoft's compression and block map writer.</summary>
    /// <param name="asset">The MSIX.</param>
    /// <param name="payloads">Original hashes mapped to signed payload paths.</param>
    /// <param name="scratch">Temporary signing directory.</param>
    /// <param name="packer">Microsoft MSIX packer path.</param>
    /// <exception cref="InvalidDataException">An executable has no matching signed payload.</exception>
    private static void ReplaceMsix(string asset, Dictionary<string, string> payloads, string scratch, string packer)
    {
        var folder = Path.Combine(scratch, Path.GetFileName(asset));
        ZipFile.ExtractToDirectory(asset, folder);
        foreach (var file in Directory.EnumerateFiles(folder, "*", SearchOption.AllDirectories))
        {
            if (!IsExecutable(file))
            {
                continue;
            }

            string hash;
            using (var source = File.OpenRead(file))
            {
                hash = Convert.ToHexString(SHA256.HashData(source));
            }

            if (!payloads.TryGetValue(hash, out var signed))
            {
                throw new InvalidDataException($"No signed payload matches {file} in {asset}.");
            }

            File.Copy(signed, file, true);
        }

        File.Delete(Path.Combine(folder, "AppxBlockMap.xml"));
        File.Delete(Path.Combine(folder, "[Content_Types].xml"));
        File.Delete(Path.Combine(folder, "AppxSignature.p7x"));
        File.Delete(asset);
        BuildTools.Run(packer, "pack", "-d", folder, "-p", asset);
        BuildTools.Run(packer, "unpack", "-ss", "-d", Path.Combine(scratch, $"verify-{Path.GetFileName(asset)}"), "-p", asset);
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
