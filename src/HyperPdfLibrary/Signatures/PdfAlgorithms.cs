// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;

namespace HyperPdfLibrary.Signatures;

/// <summary>Maps algorithm OIDs to hash algorithms and verifies X.509-style signed structures.</summary>
internal static class PdfAlgorithms
{
    /// <summary>The longest hash produced, SHA-512's.</summary>
    internal const int MaxHashLength = 64;

    /// <summary>Maps a digest algorithm OID to its hash algorithm.</summary>
    /// <param name="oid">The OID.</param>
    /// <param name="name">The hash algorithm.</param>
    /// <returns><see langword="true"/> when the OID is a supported digest.</returns>
    internal static bool TryGetHash(string? oid, out HashAlgorithmName name)
    {
        name = oid switch
        {
            "1.3.14.3.2.26" => HashAlgorithmName.SHA1,
            "2.16.840.1.101.3.4.2.1" => HashAlgorithmName.SHA256,
            "2.16.840.1.101.3.4.2.2" => HashAlgorithmName.SHA384,
            "2.16.840.1.101.3.4.2.3" => HashAlgorithmName.SHA512,
            _ => default,
        };
        return name.Name is not null;
    }

    /// <summary>Hashes data with the algorithm an OID names.</summary>
    /// <param name="oid">The digest algorithm OID.</param>
    /// <param name="data">The data.</param>
    /// <param name="destination">A buffer of at least <see cref="MaxHashLength"/> bytes.</param>
    /// <returns>The number of bytes written, or 0 when the algorithm is not supported.</returns>
    internal static int Hash(string? oid, ReadOnlySpan<byte> data, Span<byte> destination) =>
        TryGetHash(oid, out var name) ? CryptographicOperations.HashData(name, data, destination) : 0;

    /// <summary>Verifies a signature made over data with an issuer's key.</summary>
    /// <param name="data">The signed data, such as a DER TBSCertList.</param>
    /// <param name="algorithm">The signature algorithm OID.</param>
    /// <param name="signature">The signature value.</param>
    /// <param name="issuer">The certificate whose public key made the signature.</param>
    /// <returns><see langword="true"/> when the signature is good.</returns>
    internal static bool Verify(ReadOnlySpan<byte> data, string? algorithm, ReadOnlySpan<byte> signature, X509Certificate2 issuer)
    {
        var hash = SignatureHash(algorithm, out var isEcdsa);
        if (hash.Name is null)
        {
            return false;
        }

        try
        {
            if (isEcdsa)
            {
                using var ecdsa = issuer.GetECDsaPublicKey();
                return ecdsa?.VerifyData(data, signature, hash, DSASignatureFormat.Rfc3279DerSequence) == true;
            }

            using var rsa = issuer.GetRSAPublicKey();
            return rsa?.VerifyData(data, signature, hash, RSASignaturePadding.Pkcs1) == true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Maps an RSA PKCS #1 or ECDSA signature algorithm OID to its hash.</summary>
    /// <param name="algorithm">The OID.</param>
    /// <param name="isEcdsa">Whether the algorithm is ECDSA.</param>
    /// <returns>The hash, or a default name when the algorithm is not supported.</returns>
    private static HashAlgorithmName SignatureHash(string? algorithm, out bool isEcdsa)
    {
        isEcdsa = algorithm?.StartsWith("1.2.840.10045.4.", StringComparison.Ordinal) == true;
        return algorithm switch
        {
            "1.2.840.113549.1.1.5" or "1.2.840.10045.4.1" => HashAlgorithmName.SHA1,
            "1.2.840.113549.1.1.11" or "1.2.840.10045.4.3.2" => HashAlgorithmName.SHA256,
            "1.2.840.113549.1.1.12" or "1.2.840.10045.4.3.3" => HashAlgorithmName.SHA384,
            "1.2.840.113549.1.1.13" or "1.2.840.10045.4.3.4" => HashAlgorithmName.SHA512,
            _ => default,
        };
    }
}
