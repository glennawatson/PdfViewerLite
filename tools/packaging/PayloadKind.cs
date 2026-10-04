// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Tools.Packaging;

/// <summary>The kind of an installed filesystem entry.</summary>
internal enum PayloadKind
{
    /// <summary>A regular file.</summary>
    File = 0,

    /// <summary>A directory.</summary>
    Directory = 1,

    /// <summary>A symbolic link.</summary>
    Symlink = 2,
}
