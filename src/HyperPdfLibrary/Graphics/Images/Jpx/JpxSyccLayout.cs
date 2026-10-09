// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpx;

/// <summary>How the chroma of an sYCC image is sampled, for the conversion to RGB.</summary>
internal enum JpxSyccLayout
{
    /// <summary>No sYCC conversion.</summary>
    None = 0,

    /// <summary>Full-resolution chroma (4:4:4).</summary>
    Full = 1,

    /// <summary>Chroma halved horizontally (4:2:2).</summary>
    HalfWidth = 2,

    /// <summary>Chroma halved both ways (4:2:0).</summary>
    Quarter = 3,
}
