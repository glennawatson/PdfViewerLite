// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Formats.Asn1;
using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// The parts of an X.509-style signed structure (a certificate, CRL or OCSP basic response): the signed body, the
/// signature algorithm and the signature, followed by anything else in the outer sequence.
/// </summary>
/// <param name="Body">The DER of the signed body.</param>
/// <param name="Algorithm">The signature algorithm OID.</param>
/// <param name="Signature">The signature value.</param>
/// <param name="Rest">A reader over what follows the signature in the outer sequence.</param>
[DebuggerDisplay("PdfSignedStructure: {Algorithm}")]
internal readonly record struct PdfSignedStructure(ReadOnlyMemory<byte> Body, string Algorithm, byte[] Signature, AsnReader Rest)
{
    /// <summary>Splits a DER signed structure.</summary>
    /// <param name="der">The DER bytes.</param>
    /// <returns>The parts.</returns>
    /// <exception cref="AsnContentException">The bytes are not a signed structure.</exception>
    internal static PdfSignedStructure Read(ReadOnlyMemory<byte> der)
    {
        var outer = new AsnReader(der, AsnEncodingRules.DER).ReadSequence();
        var body = outer.ReadEncodedValue();
        var algorithm = outer.ReadSequence().ReadObjectIdentifier();
        var signature = outer.ReadBitString(out _);
        return new(body, algorithm, signature, outer);
    }

    /// <summary>Verifies the signature with an issuer's key.</summary>
    /// <param name="issuer">The issuer.</param>
    /// <returns><see langword="true"/> when the signature is good.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal bool IsSignedBy(X509Certificate2 issuer) => PdfAlgorithms.Verify(Body.Span, Algorithm, Signature, issuer);
}
