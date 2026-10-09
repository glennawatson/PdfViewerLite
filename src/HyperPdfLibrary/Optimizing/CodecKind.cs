// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The image codec at the end of an image's filter chain.</summary>
internal enum CodecKind
{
    /// <summary>Only byte filters, or none.</summary>
    Bytes = 0,

    /// <summary>The DCTDecode filter: JPEG.</summary>
    Jpeg = 1,

    /// <summary>The JPXDecode filter: JPEG 2000.</summary>
    Jpeg2000 = 2,

    /// <summary>The JBIG2Decode filter.</summary>
    Jbig2 = 3,

    /// <summary>The CCITTFaxDecode filter.</summary>
    Fax = 4,

    /// <summary>A /Crypt filter, which the optimiser leaves alone.</summary>
    Crypt = 5,
}
