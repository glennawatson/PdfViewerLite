// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>One node of a tag tree (ISO 15444-1 B.10.2).</summary>
internal record struct JpxTagNode
{
    /// <summary>Gets or sets the node's value, or <see cref="JpxTagTree.Unknown"/> while not yet decoded.</summary>
    internal int Value { get; set; }

    /// <summary>Gets or sets the lower bound decoded so far.</summary>
    internal int Low { get; set; }
}
