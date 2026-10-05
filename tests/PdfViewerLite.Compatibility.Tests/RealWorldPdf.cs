// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>One real-world PDF in the compatibility corpus.</summary>
/// <param name="Id">A short name, also the cached file's name.</param>
/// <param name="Source">The repository the file comes from.</param>
/// <param name="Url">Where to download it, pinned to a commit.</param>
/// <param name="Sha256">The file's SHA-256, checked after download.</param>
/// <param name="Bytes">The file's size.</param>
/// <param name="Tags">What the document exercises, such as <c>two-column</c>, <c>forms</c> or <c>damaged</c>.</param>
/// <param name="Licence">The licence of the source repository.</param>
/// <param name="GroundTruthUrl">A Markdown transcript in reading order, when the source has one.</param>
/// <param name="Password">The password that opens it, when it has one.</param>
/// <param name="Unreadable">Whether the file is too broken to open, so the test checks it fails cleanly instead.</param>
[DebuggerDisplay("RealWorldPdf: {Id}")]
public sealed record RealWorldPdf(
    string Id,
    string Source,
    Uri Url,
    string Sha256,
    long Bytes,
    IReadOnlyList<string> Tags,
    string Licence,
    Uri? GroundTruthUrl,
    string? Password,
    bool Unreadable)
{
    /// <summary>Determines whether the document carries a tag.</summary>
    /// <param name="tag">The tag.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Has(string tag) => Tags.Contains(tag, StringComparer.Ordinal);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public override string ToString() => Id;
}
