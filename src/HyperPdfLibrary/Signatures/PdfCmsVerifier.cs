// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Formats.Asn1;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Signatures;

/// <summary>
/// Checks a signature's CMS container with <see cref="SignedCms"/>: the message digest over the byte ranges, the signer's
/// signature, its signed signing time and timestamp, and the signer's chain. Never throws on damaged input; a container
/// that cannot be read gives a <see cref="PdfCmsStatus.Malformed"/> result.
/// </summary>
internal static class PdfCmsVerifier
{
    /// <summary>The length of a SHA-1 hash.</summary>
    private const int Sha1Length = 20;

    /// <summary>The tag and length bytes of a short OCTET STRING.</summary>
    private const int OctetStringHeader = 2;

    /// <summary>The numbers per byte range: offset and length.</summary>
    private const int Pair = 2;

    /// <summary>The unsigned attribute holding a signature's RFC 3161 timestamp token.</summary>
    private const string TimestampTokenOid = "1.2.840.113549.1.9.16.2.14";

    /// <summary>The SHA-1 OID, the digest of <c>adbe.pkcs7.sha1</c> content.</summary>
    private const string Sha1Oid = "1.3.14.3.2.26";

    /// <summary>Checks a signature.</summary>
    /// <param name="details">The signature.</param>
    /// <param name="range">The byte range check.</param>
    /// <param name="file">The file.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    internal static PdfCmsResult Verify(PdfSignatureDetails details, in PdfByteRangeCheck range, PdfByteSource file, CmsContext context)
    {
        var contents = details.Field.Contents;
        if (contents.Length == 0)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Unsigned, "The field is not signed.");
        }

        if (!range.IsValid)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.ByteRangeInvalid, $"The byte range is not valid: {range.Status}.");
        }

        if (!TryGetEncodedLength(contents, out var length))
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, "The signature is not a DER-encoded container.");
        }

        var signed = ReadSigned(file, details.Field.ByteRange, range.SignedLength);
        try
        {
            return details.Format switch
            {
                PdfSignatureFormat.Rfc3161 => VerifyDocumentTimestamp(signed, contents.AsMemory(0, length), context),
                PdfSignatureFormat.X509RsaSha1 => PdfCmsResult.Failed(PdfCmsStatus.Unsupported, "adbe.x509.rsa_sha1 signatures are not checked."),
                PdfSignatureFormat.Pkcs7Sha1 => VerifyContainer(signed, contents.AsSpan(0, length), false, context),
                _ => VerifyContainer(signed, contents.AsSpan(0, length), true, context),
            };
        }
        catch (Exception ex) when (ex is CryptographicException or AsnContentException or InvalidOperationException)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, ex.Message);
        }
    }

    /// <summary>Gets the length of the DER value at the start of /Contents, so the zero padding after it is ignored.</summary>
    /// <param name="contents">The padded contents.</param>
    /// <param name="length">The encoded length.</param>
    /// <returns><see langword="true"/> when the contents start with a complete value.</returns>
    internal static bool TryGetEncodedLength(ReadOnlySpan<byte> contents, out int length)
    {
        try
        {
            _ = AsnDecoder.ReadEncodedValue(contents, AsnEncodingRules.BER, out _, out _, out length);
            return true;
        }
        catch (AsnContentException)
        {
            length = 0;
            return false;
        }
    }

    /// <summary>
    /// Reads the signed parts of the file into one buffer, through the file's source. The buffer lives only for the check:
    /// <see cref="SignedCms"/> takes detached content as one array.
    /// </summary>
    /// <param name="file">The file.</param>
    /// <param name="range">The byte range, already checked.</param>
    /// <param name="length">The total signed length.</param>
    /// <returns>The signed bytes.</returns>
    private static byte[] ReadSigned(PdfByteSource file, long[] range, long length)
    {
        var signed = new byte[length];
        var written = 0;
        for (var i = 0; i < range.Length; i += Pair)
        {
            var part = signed.AsSpan(written, (int)range[i + 1]);
            written += file.Read(range[i], part);
        }

        return signed;
    }

    /// <summary>Checks a CMS signature: detached over the signed bytes, or enveloping their SHA-1 hash.</summary>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="der">The CMS container.</param>
    /// <param name="detached">Whether the content is the signed bytes rather than their SHA-1 hash.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    private static PdfCmsResult VerifyContainer(byte[] signed, ReadOnlySpan<byte> der, bool detached, CmsContext context)
    {
        var cms = detached ? new SignedCms(new ContentInfo(signed), true) : new SignedCms();
        cms.Decode(der);
        if (cms.SignerInfos.Count == 0)
        {
            return PdfCmsResult.Failed(PdfCmsStatus.MissingSigner, "The signature has no signer.");
        }

        var signer = cms.SignerInfos[0];
        var certificate = signer.Certificate ?? Find(signer.SignerIdentifier, context.StoreCertificates);
        var signatureValid = CheckSignature(signer, context.StoreCertificates);
        var digestValid = detached ? DigestMatches(signer, signed, signatureValid) : EnvelopedDigestMatches(cms, signed) && signatureValid;
        var timestamp = ReadTimestamp(signer, context);
        var algorithm = signer.DigestAlgorithm.Value ?? string.Empty;
        if (certificate is null)
        {
            const string Missing = "The signer's certificate is not in the signature or the security store.";
            return new(PdfCmsStatus.MissingSigner, digestValid, false, null, algorithm, SigningTime(signer), timestamp, null, Missing);
        }

        var chain = PdfChainBuilder.Build(certificate, cms.Certificates, context, CheckTime(context, timestamp));
        return new(PdfCmsStatus.Checked, digestValid, signatureValid, certificate, algorithm, SigningTime(signer), timestamp, chain, string.Empty);
    }

    /// <summary>Gets the time to check a signer's chain at: the option, a valid timestamp's time, or now.</summary>
    /// <param name="context">The store and options.</param>
    /// <param name="timestamp">The signature's timestamp, or <see langword="null"/>.</param>
    /// <returns>The time.</returns>
    private static DateTimeOffset CheckTime(CmsContext context, PdfTimestampResult? timestamp)
    {
        if (context.Options.VerificationTime is { } time)
        {
            return time;
        }

        return timestamp is { IsValid: true } ? timestamp.Time : context.Options.Clock.GetUtcNow();
    }

    /// <summary>Checks a document timestamp: a timestamp token whose imprint is the hash of the signed bytes.</summary>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="der">The token.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The result.</returns>
    private static PdfCmsResult VerifyDocumentTimestamp(byte[] signed, ReadOnlyMemory<byte> der, CmsContext context)
    {
        if (!Rfc3161TimestampToken.TryDecode(der, out var stamp, out _))
        {
            return PdfCmsResult.Failed(PdfCmsStatus.Malformed, "The document timestamp could not be read.");
        }

        var info = stamp.TokenInfo;
        var oid = info.HashAlgorithmId.Value;
        Span<byte> actual = stackalloc byte[PdfAlgorithms.MaxHashLength];
        var length = PdfAlgorithms.Hash(oid, signed, actual);
        var digestValid = length > 0 && CryptographicOperations.FixedTimeEquals(actual[..length], info.GetMessageHash().Span);
        var signatureValid = stamp.VerifySignatureForData(signed, out var authority, context.StoreCertificates);
        var at = context.Options.VerificationTime ?? info.Timestamp;
        var chain = authority is null ? null : PdfChainBuilder.Build(authority, stamp.AsSignedCms().Certificates, context, at);
        var result = new PdfTimestampResult(info.Timestamp, signatureValid, authority, chain);
        return new(PdfCmsStatus.Checked, digestValid, signatureValid, authority, oid ?? string.Empty, info.Timestamp, result, chain, string.Empty);
    }

    /// <summary>Checks the signer's signature without judging the certificate.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="extra">Extra certificates to find the signer's in.</param>
    /// <returns><see langword="true"/> when the signature is good.</returns>
    private static bool CheckSignature(SignerInfo signer, X509Certificate2Collection extra)
    {
        try
        {
            signer.CheckSignature(extra, true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Compares the signed messageDigest attribute with the hash of the signed bytes.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <param name="signatureValid">The signature result, used when there are no signed attributes and the signature covers the content directly.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool DigestMatches(SignerInfo signer, byte[] signed, bool signatureValid)
    {
        if (FindMessageDigest(signer) is not { } expected)
        {
            return signer.SignedAttributes.Count == 0 && signatureValid;
        }

        Span<byte> actual = stackalloc byte[PdfAlgorithms.MaxHashLength];
        var length = PdfAlgorithms.Hash(signer.DigestAlgorithm.Value, signed, actual);
        return length > 0 && CryptographicOperations.FixedTimeEquals(actual[..length], expected);
    }

    /// <summary>Finds the signed messageDigest attribute.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The digest, or <see langword="null"/>.</returns>
    private static byte[]? FindMessageDigest(SignerInfo signer)
    {
        foreach (var attribute in signer.SignedAttributes)
        {
            foreach (var value in attribute.Values)
            {
                if (value is Pkcs9MessageDigest digest)
                {
                    return digest.MessageDigest;
                }
            }
        }

        return null;
    }

    /// <summary>Compares an <c>adbe.pkcs7.sha1</c> container's content with the SHA-1 of the signed bytes.</summary>
    /// <param name="cms">The decoded container.</param>
    /// <param name="signed">The signed bytes.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    private static bool EnvelopedDigestMatches(SignedCms cms, byte[] signed)
    {
        Span<byte> actual = stackalloc byte[Sha1Length];
        _ = PdfAlgorithms.Hash(Sha1Oid, signed, actual);
        var content = cms.ContentInfo.Content.AsSpan();

        // Writers store the hash either bare or wrapped in an OCTET STRING.
        return content.SequenceEqual(actual) || (content.Length == Sha1Length + OctetStringHeader && content[OctetStringHeader..].SequenceEqual(actual));
    }

    /// <summary>Reads and checks the timestamp token in a signer's unsigned attributes.</summary>
    /// <param name="signer">The signer.</param>
    /// <param name="context">The store and options.</param>
    /// <returns>The timestamp, or <see langword="null"/> when there is none.</returns>
    private static PdfTimestampResult? ReadTimestamp(SignerInfo signer, CmsContext context)
    {
        foreach (var attribute in signer.UnsignedAttributes)
        {
            if (attribute.Oid.Value != TimestampTokenOid || attribute.Values.Count == 0)
            {
                continue;
            }

            if (!Rfc3161TimestampToken.TryDecode(attribute.Values[0].RawData, out var stamp, out _))
            {
                return new(DateTimeOffset.MinValue, false, null, null);
            }

            var valid = stamp.VerifySignatureForSignerInfo(signer, out var authority, context.StoreCertificates);
            var time = stamp.TokenInfo.Timestamp;
            var chain = authority is null ? null : PdfChainBuilder.Build(authority, stamp.AsSignedCms().Certificates, context, context.Options.VerificationTime ?? time);
            return new(time, valid, authority, chain);
        }

        return null;
    }

    /// <summary>Reads the signed signing time attribute.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    private static DateTimeOffset? SigningTime(SignerInfo signer)
    {
        foreach (var attribute in signer.SignedAttributes)
        {
            foreach (var value in attribute.Values)
            {
                if (value is Pkcs9SigningTime time)
                {
                    return new DateTimeOffset(time.SigningTime.ToUniversalTime(), TimeSpan.Zero);
                }
            }
        }

        return null;
    }

    /// <summary>Finds a signer's certificate among extra certificates.</summary>
    /// <param name="identifier">The signer's identifier.</param>
    /// <param name="candidates">The candidates.</param>
    /// <returns>The certificate, or <see langword="null"/>.</returns>
    private static X509Certificate2? Find(SubjectIdentifier identifier, X509Certificate2Collection candidates)
    {
        foreach (var candidate in candidates)
        {
            if (identifier.MatchesCertificate(candidate))
            {
                return candidate;
            }
        }

        return null;
    }
}
