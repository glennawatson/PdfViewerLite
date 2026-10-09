// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Raster;

/// <summary>
/// Reads a file's PDF/R claim: a <c>%PDF-raster-x.y</c> comment near the end of the file. The comment form comes from
/// the PDF Association's description and third-party validator documentation; the text of ISO 23504-1 was not
/// available, so the exact rule for its position is inferred. A claim in the last <see cref="TailBytes"/> is found.
/// </summary>
internal static class RasterClaimReader
{
    /// <summary>How many bytes at the end of the file are searched.</summary>
    internal const int TailBytes = 8192;

    /// <summary>The longest claim text read, past the leading percent sign.</summary>
    private const int MaxClaimLength = 32;

    /// <summary>Gets the claim comment's start, without the percent sign.</summary>
    private static ReadOnlySpan<byte> Prefix => "%PDF-raster-"u8;

    /// <summary>Reads the claim.</summary>
    /// <param name="source">The file.</param>
    /// <returns>The claim without the percent sign, for example "PDF-raster-1.0"; null when there is none.</returns>
    internal static string? Read(PdfByteSource source)
    {
        var length = (int)Math.Min(TailBytes, source.Length);
        using var tail = source.Lease(source.Length - length, length);
        var span = tail.Span;
        var at = span.LastIndexOf(Prefix);
        if (at < 0)
        {
            return null;
        }

        var rest = span[(at + 1)..];
        var end = rest.IndexOfAny("\r\n"u8);
        if (end is < 0 or > MaxClaimLength)
        {
            end = Math.Min(rest.Length, MaxClaimLength);
        }

        var claim = Encoding.ASCII.GetString(rest[..end]).Trim();
        return claim.Length > Prefix.Length - 1 ? claim : null;
    }
}
