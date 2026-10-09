// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>The result of building a certificate chain for a signer or timestamp authority.</summary>
/// <param name="IsTrusted">Whether the chain reaches a trusted root with no errors.</param>
/// <param name="Status">The combined chain status flags; revocation flags appear only when online checks are allowed.</param>
/// <param name="Certificates">The chain, leaf first.</param>
/// <param name="Revocation">The leaf's revocation status from the data saved in the document.</param>
/// <param name="VerifiedAt">The time the chain was checked at.</param>
[DebuggerDisplay("PdfChainResult: trusted {IsTrusted} {Status}")]
public sealed record PdfChainResult(
    bool IsTrusted,
    X509ChainStatusFlags Status,
    X509Certificate2[] Certificates,
    PdfRevocationStatus Revocation,
    DateTimeOffset VerifiedAt);
