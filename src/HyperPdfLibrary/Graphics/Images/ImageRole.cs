// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>What an image stream is decoded as.</summary>
internal enum ImageRole
{
    /// <summary>A painted image, with its masks applied.</summary>
    Image = 0,

    /// <summary>A soft mask: decoded as DeviceGray with no masks of its own.</summary>
    SoftMask = 1,

    /// <summary>An explicit /Mask stream: decoded as a stencil mask.</summary>
    StencilMask = 2,
}
