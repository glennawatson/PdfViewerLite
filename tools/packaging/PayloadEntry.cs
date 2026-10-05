// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>One installed filesystem entry.</summary>
/// <param name="Path">The absolute install path using forward slashes.</param>
/// <param name="Kind">The entry kind.</param>
/// <param name="Mode">The Unix permission bits.</param>
/// <param name="Source">The source file on disk for a regular file; otherwise null.</param>
/// <param name="Target">The link target for a symbolic link; otherwise null.</param>
internal sealed record PayloadEntry(string Path, PayloadKind Kind, int Mode, string? Source, string? Target)
{
    /// <summary>Gets the content length in bytes.</summary>
    /// <returns>The file length, the link target length, or zero for a directory.</returns>
    internal long GetLength() => Kind switch
    {
        PayloadKind.File => new FileInfo(Source!).Length,
        PayloadKind.Symlink => Encoding.UTF8.GetByteCount(Target!),
        PayloadKind.Directory => 0,
        _ => 0,
    };
}
