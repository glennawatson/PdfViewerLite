// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>A certificate's revocation status, from the OCSP responses and CRLs saved in the document.</summary>
public enum PdfRevocationStatus
{
    /// <summary>No saved response or list covers the certificate, or none could be verified.</summary>
    Unknown = 0,

    /// <summary>A verified saved response or list says the certificate is not revoked.</summary>
    NotRevoked = 1,

    /// <summary>A verified saved response or list says the certificate is revoked.</summary>
    Revoked = 2,
}
