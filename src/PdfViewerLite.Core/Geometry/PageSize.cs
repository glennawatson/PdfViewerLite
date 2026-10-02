// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Geometry;

/// <summary>The unrotated size of a page in PDF points (1/72 inch).</summary>
/// <param name="Width">The page width in points.</param>
/// <param name="Height">The page height in points.</param>
[DebuggerDisplay("{Width} x {Height}")]
public readonly record struct PageSize(float Width, float Height)
{
    /// <summary>Gets the size after applying a rotation.</summary>
    /// <param name="rotation">The rotation to apply.</param>
    /// <returns>The rotated size.</returns>
    public PageSize Rotate(PageRotation rotation) =>
        rotation is PageRotation.Rotate90 or PageRotation.Rotate270 ? this with { Width = Height, Height = Width } : this;
}
