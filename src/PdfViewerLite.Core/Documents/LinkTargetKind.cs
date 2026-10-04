// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Core.Documents;

/// <summary>The kind of a <see cref="LinkTarget"/>.</summary>
public enum LinkTargetKind
{
    /// <summary>No target.</summary>
    None = 0,

    /// <summary>A page in the same document.</summary>
    Page = 1,

    /// <summary>An external URI.</summary>
    Uri = 2,
}
