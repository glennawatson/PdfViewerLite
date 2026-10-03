// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>A temporary PDF path, deleted on dispose.</summary>
/// <param name="Path">The path.</param>
[DebuggerDisplay("{Path}")]
internal sealed record TempFile(string Path) : IDisposable
{
    /// <inheritdoc/>
    public void Dispose() => File.Delete(Path);

    /// <summary>Creates a new temporary path.</summary>
    /// <returns>The file.</returns>
    internal static TempFile Create() => new(System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfviewerlite-corpus-{Guid.NewGuid():N}.pdf"));
}
