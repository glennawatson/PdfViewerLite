// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>The encoding of a signature's /Contents, from its /SubFilter.</summary>
public enum PdfSignatureFormat
{
    /// <summary>The sub-filter is missing or not recognised; checked as a detached CMS signature.</summary>
    Unknown = 0,

    /// <summary><c>adbe.pkcs7.detached</c>: a CMS signature over the signed bytes.</summary>
    Pkcs7Detached = 1,

    /// <summary><c>ETSI.CAdES.detached</c>: a CAdES (PAdES) CMS signature over the signed bytes.</summary>
    CadesDetached = 2,

    /// <summary><c>adbe.pkcs7.sha1</c>: a CMS signature whose content is the SHA-1 hash of the signed bytes.</summary>
    Pkcs7Sha1 = 3,

    /// <summary><c>ETSI.RFC3161</c>: a document timestamp token over the signed bytes.</summary>
    Rfc3161 = 4,

    /// <summary><c>adbe.x509.rsa_sha1</c>: a bare RSA signature with the certificate in /Cert; not checked.</summary>
    X509RsaSha1 = 5,
}
