// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Settings;

/// <summary>A tab remembered between runs.</summary>
[DebuggerDisplay("{FilePath} p{PageIndex}")]
public sealed class SessionTab
{
    /// <summary>Gets or sets the file path.</summary>
    public string FilePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the page that was showing.</summary>
    public int PageIndex { get; set; }

    /// <summary>Gets or sets a value indicating whether this was the selected tab.</summary>
    public bool IsSelected { get; set; }
}
