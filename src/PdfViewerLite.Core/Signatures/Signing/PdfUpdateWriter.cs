// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace PdfViewerLite.Core.Signatures.Signing;

/// <summary>Writes incremental updates: new or redefined objects appended after the original bytes, which stay untouched.</summary>
internal static class PdfUpdateWriter
{
    /// <summary>Writes the objects, a cross-reference table and a trailer pointing back at the previous one.</summary>
    /// <param name="structure">The file structure.</param>
    /// <param name="objects">The objects by number.</param>
    /// <param name="root">The catalog's number.</param>
    /// <param name="size">The new object count.</param>
    /// <returns>The update's bytes.</returns>
    internal static byte[] Serialize(PdfStructure structure, SortedDictionary<int, string> objects, int root, int size)
    {
        var start = structure.File.Length;
        var update = new StringBuilder("\n");
        var offsets = new SortedDictionary<int, int>();
        foreach (var (number, body) in objects)
        {
            offsets[number] = start + update.Length;
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 0 obj\n{body}\nendobj\n");
        }

        var xref = start + update.Length;
        _ = update.Append("xref\n0 1\n0000000000 65535 f \n");
        foreach (var (number, offset) in offsets)
        {
            _ = update.Append(CultureInfo.InvariantCulture, $"{number} 1\n{offset:D10} 00000 n \n");
        }

        _ = update.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {size} /Root {root} 0 R /Prev {structure.StartXref}");
        CopyTrailerEntry(structure.Trailer, "Info"u8, update);
        CopyTrailerEntry(structure.Trailer, "ID"u8, update);
        _ = update.Append(CultureInfo.InvariantCulture, $" >>\nstartxref\n{xref}\n%%EOF\n");
        return Encoding.Latin1.GetBytes(update.ToString());
    }

    /// <summary>Copies a trailer entry, such as the document id, into the new trailer.</summary>
    /// <param name="trailer">The previous trailer.</param>
    /// <param name="key">The key.</param>
    /// <param name="update">The update being written.</param>
    private static void CopyTrailerEntry(byte[] trailer, ReadOnlySpan<byte> key, StringBuilder update)
    {
        var at = PdfSyntax.FindKey(trailer, 0, key);
        if (at < 0)
        {
            return;
        }

        _ = update.Append(CultureInfo.InvariantCulture, $" /{Encoding.ASCII.GetString(key)} {Encoding.Latin1.GetString(trailer.AsSpan(at, PdfSyntax.ValueEnd(trailer, at) - at))}");
    }
}
