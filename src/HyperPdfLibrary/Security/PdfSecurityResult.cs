// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Security;

/// <summary>The outcome of opening an encrypted document.</summary>
public enum PdfSecurityResult
{
    /// <summary>The password was accepted.</summary>
    Success = 0,

    /// <summary>The password was wrong, or a password is needed.</summary>
    WrongPassword = 1,

    /// <summary>The document uses a security handler other than the standard one.</summary>
    UnsupportedHandler = 2,

    /// <summary>The document is encrypted for certificates, and no certificate given can open it.</summary>
    CertificateRequired = 3,

    /// <summary>The encryption dictionary is damaged, for example a missing or oversized /Recipients.</summary>
    Damaged = 4,
}
