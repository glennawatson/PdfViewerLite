// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Redaction;

/// <summary>What redaction does to an image that a redacted area touches.</summary>
public enum PdfRedactionImageMode
{
    /// <summary>Leave images alone.</summary>
    None = 0,

    /// <summary>Remove the whole image when the area touches it anywhere, whether or not that part shows.</summary>
    Remove = 1,

    /// <summary>Blank only the pixels under the area. Colour and mask data stay valid; images that cannot be decoded are removed.</summary>
    BlankPixels = 2,

    /// <summary>Remove the whole image when the area touches a part of it that shows; leave it alone when only clipped-away parts are touched.</summary>
    RemoveUnlessInvisible = 3,
}
