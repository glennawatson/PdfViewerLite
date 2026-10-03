// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.Core.Signatures;

/// <summary>A checked digital signature.</summary>
/// <param name="Index">The signature's index in the document.</param>
/// <param name="SignerName">The signer's name from the certificate, or an empty string when unknown.</param>
/// <param name="Issuer">Who issued the signer's certificate.</param>
/// <param name="SigningTime">When it was signed, if known.</param>
/// <param name="Reason">The reason the signer gave, or an empty string.</param>
/// <param name="Integrity">Whether the signed content is unchanged.</param>
/// <param name="IsTrusted">Whether the signer's certificate chains to a certificate this computer trusts.</param>
/// <param name="Detail">A short explanation when the signature could not be checked or is not trusted.</param>
[DebuggerDisplay("{SignerName}: {Integrity}, trusted {IsTrusted}")]
public sealed record DocumentSignature(int Index, string SignerName, string Issuer, DateTimeOffset? SigningTime, string Reason, SignatureIntegrity Integrity, bool IsTrusted, string Detail)
{
    /// <summary>Gets a one-line summary in plain words.</summary>
    public string Summary => Integrity switch
    {
        SignatureIntegrity.Intact when IsTrusted => "Valid signature; the document has not changed since it was signed.",
        SignatureIntegrity.Intact => "The document has not changed since it was signed, but the signer's certificate is not trusted on this computer.",
        SignatureIntegrity.ChangedAfterSigning => "The signed part is unchanged; the document was changed after signing.",
        SignatureIntegrity.Invalid => "The signature does not match: the signed content was altered or the signature is damaged.",
        _ => "This signature could not be checked.",
    };
}
