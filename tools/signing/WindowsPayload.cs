// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Updates Windows release packages with signed executable payloads.</summary>
internal static class WindowsPayload
{
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
                MsiPayload.Replace(asset, payloads, scratch);
            }
        }

        foreach (var asset in assets)
        {
            if (asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
            {
                MsixWriter.Replace(asset, payloads);
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

    /// <summary>Replaces executable entries in portable ZIP archives.</summary>
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

    /// <summary>Identifies executable archive entries.</summary>
    /// <param name="name">The entry name.</param>
    /// <returns>Whether the entry is an EXE or DLL.</returns>
    private static bool IsExecutable(string name) => name.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) || name.EndsWith(".dll", StringComparison.OrdinalIgnoreCase);
}
