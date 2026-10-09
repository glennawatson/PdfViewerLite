// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.IO;

/// <summary>How a document opened from a path reads its file.</summary>
public enum PdfSourceKind
{
    /// <summary>
    /// Maps the file, falling back to <see cref="Stream"/> when the file cannot be mapped. Windows uses <see cref="Stream"/>,
    /// as it refuses to delete a mapped file, which saving over an open document needs.
    /// </summary>
    Automatic = 0,

    /// <summary>Reads the whole file into memory first.</summary>
    Memory = 1,

    /// <summary>Maps the file read-only; see <see cref="MappedPdfByteSource"/>.</summary>
    Mapped = 2,

    /// <summary>Reads the file through a handle and a bounded page cache; see <see cref="StreamPdfByteSource"/>.</summary>
    Stream = 3,
}
