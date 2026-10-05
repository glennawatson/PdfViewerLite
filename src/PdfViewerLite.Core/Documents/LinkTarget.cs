// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.Core.Documents;

/// <summary>Where a link or outline entry leads.</summary>
/// <param name="Kind">The kind of target.</param>
/// <param name="PageIndex">The zero based destination page, or -1.</param>
/// <param name="Location">The destination point within the page, when specified.</param>
/// <param name="Uri">The external URI, when <paramref name="Kind"/> is <see cref="LinkTargetKind.Uri"/>.</param>
[DebuggerDisplay("LinkTarget: {Kind} {PageIndex} {Uri}")]
public readonly record struct LinkTarget(LinkTargetKind Kind, int PageIndex, PagePoint? Location, string? Uri)
{
    /// <summary>Gets a target that leads nowhere.</summary>
    public static LinkTarget None => new(LinkTargetKind.None, -1, null, null);

    /// <summary>Creates a page destination.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    /// <returns>The target.</returns>
    public static LinkTarget ForPage(int pageIndex) => new(LinkTargetKind.Page, pageIndex, null, null);

    /// <summary>Creates a page destination with a location.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    /// <param name="location">The location within the page.</param>
    /// <returns>The target.</returns>
    public static LinkTarget ForPage(int pageIndex, PagePoint? location) => new(LinkTargetKind.Page, pageIndex, location, null);

    /// <summary>Creates an external URI target.</summary>
    /// <param name="uri">The URI.</param>
    /// <returns>The target.</returns>
    public static LinkTarget ForUri(string uri) => new(LinkTargetKind.Uri, -1, null, uri);
}
