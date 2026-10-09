// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A run of items on one line, left to right, with no column gap inside it.</summary>
[DebuggerDisplay("LayoutLine: {Items.Count} items")]
internal sealed class LayoutLine
{
    /// <summary>Gets the items, left to right once the line is finished.</summary>
    internal List<LayoutItem> Items { get; } = [];

    /// <summary>Gets the box around the items.</summary>
    internal PdfViewerRect Bounds { get; private set; }

    /// <summary>Adds an item.</summary>
    /// <param name="item">The item.</param>
    internal void Add(in LayoutItem item)
    {
        Items.Add(item);
        Bounds = Bounds.Union(item.Bounds);
    }
}
