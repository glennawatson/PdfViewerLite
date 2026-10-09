// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>Whether a signature's cryptographic container could be checked.</summary>
public enum PdfCmsStatus
{
    /// <summary>The container was decoded and checked; see the digest and signature results.</summary>
    Checked = 0,

    /// <summary>The byte range is not valid, so the signed bytes are unknown.</summary>
    ByteRangeInvalid = 1,

    /// <summary>The field is not signed.</summary>
    Unsigned = 2,

    /// <summary>The /Contents could not be decoded as the format requires.</summary>
    Malformed = 3,

    /// <summary>The container holds no signer, or the signer's certificate cannot be found.</summary>
    MissingSigner = 4,

    /// <summary>The format is not supported.</summary>
    Unsupported = 5,
}
