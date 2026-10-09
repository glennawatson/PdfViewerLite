// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signatures;

/// <summary>The OCSPResponseStatus values (RFC 6960).</summary>
internal enum OcspStatus
{
    /// <summary>The response holds valid confirmations.</summary>
    Successful = 0,

    /// <summary>The request was malformed.</summary>
    MalformedRequest = 1,

    /// <summary>The responder had an internal error.</summary>
    InternalError = 2,

    /// <summary>The responder asked to try later.</summary>
    TryLater = 3,

    /// <summary>The request must be signed.</summary>
    SignatureRequired = 5,

    /// <summary>The requester is not authorised.</summary>
    Unauthorized = 6,
}
