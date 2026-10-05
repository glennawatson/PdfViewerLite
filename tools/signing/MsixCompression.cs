// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.IO.Compression;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using System.Text;
using System.Xml.Linq;

using SharpCompress.Common;
using SharpCompress.Providers;
using SharpCompress.Writers.Zip;

using Deflate = SharpCompress.Compressors.Deflate;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Repackages MSIX payloads with independently compressed hash blocks.</summary>
internal static class MsixCompression
{
    /// <summary>Uncompressed bytes in each MSIX hash block.</summary>
    private const int BlockLength = 65_536;

    /// <summary>ZIP local header length before the UTF-8 filename.</summary>
    private const int LocalHeaderLength = 30;

    /// <summary>Package block map entry name.</summary>
    private const string BlockMapName = "AppxBlockMap.xml";

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

            using var provider = new BlockDeflateProvider();
            using var destination = File.Create(temporary);
            using var writer = new ZipWriter(destination, new ZipWriterOptions(CompressionType.Deflate) { Providers = CompressionProviderRegistry.Empty.With(provider), });
            var buffer = new byte[BlockLength];
            foreach (var item in archive.Entries)
            {
                if (item.FullName is BlockMapName or "AppxSignature.p7x")
                {
                    continue;
                }

                _ = files.TryGetValue(item.FullName, out var file);
                WriteEntry(writer, provider, item, file, payloads, buffer);
            }

            using var target = writer.WriteToStream(BlockMapName, new());
            map.Save(target);
        }

        File.Move(temporary, asset, true);
    }

    /// <summary>Writes a payload and records each independently compressed block.</summary>
    /// <param name="writer">The package writer.</param>
    /// <param name="provider">The block compression provider.</param>
    /// <param name="entry">The original archive entry.</param>
    /// <param name="file">The entry's block map element.</param>
    /// <param name="payloads">Original hashes mapped to signed executable paths.</param>
    /// <param name="buffer">The reusable uncompressed block buffer.</param>
    private static void WriteEntry(ZipWriter writer, BlockDeflateProvider provider, ZipArchiveEntry entry, XElement? file, Dictionary<string, string> payloads, byte[] buffer)
    {
        var signed = FindReplacement(entry, payloads);
        using var source = signed is null ? entry.Open() : File.OpenRead(signed);
        using var target = writer.WriteToStream(entry.FullName, new ZipWriterEntryOptions { ModificationDateTime = entry.LastWriteTime.DateTime });
        if (file is not null)
        {
            file.SetAttributeValue("Size", signed is null ? entry.Length : source.Length);
            file.SetAttributeValue("LfhSize", LocalHeaderLength + Encoding.UTF8.GetByteCount(entry.FullName));
            file.RemoveNodes();
        }

        long previous = 0;
        int count;
        while ((count = source.ReadAtLeast(buffer, buffer.Length, throwOnEndOfStream: false)) > 0)
        {
            target.Write(buffer, 0, count);
            var total = provider.TotalOut;
            file?.Add(new XElement(
                file.Name.Namespace + "Block",
                new XAttribute("Hash", Convert.ToBase64String(SHA256.HashData(buffer.AsSpan(0, count)))),
                new XAttribute("Size", total - previous)));
            previous = total;
        }

        // Disposing the compressor writes the final DEFLATE marker, outside the block byte counts.
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

    /// <summary>Flushes and resets the DEFLATE dictionary after every block write.</summary>
    private sealed class BlockDeflateProvider : CompressionProviderBase, IDisposable
    {
        /// <summary>The current entry's compressor.</summary>
        private Deflate.DeflateStream? _current;

        /// <inheritdoc />
        public override CompressionType CompressionType => CompressionType.Deflate;

        /// <inheritdoc />
        public override bool SupportsCompression => true;

        /// <inheritdoc />
        public override bool SupportsDecompression => false;

        /// <summary>Gets compressed bytes written for the current entry.</summary>
        internal long TotalOut => _current!.TotalOut;

        /// <inheritdoc />
        public override Stream CreateCompressStream(Stream destination, int compressionLevel) => _current = new(
            destination,
            SharpCompress.Compressors.CompressionMode.Compress,
            (Deflate.CompressionLevel)compressionLevel)
        { FlushMode = Deflate.FlushType.Full, };

        /// <inheritdoc />
        public override Stream CreateDecompressStream(Stream source) => throw new NotSupportedException();

        /// <summary>Releases the current entry's compressor.</summary>
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        public void Dispose() => _current?.Dispose();
    }
}
