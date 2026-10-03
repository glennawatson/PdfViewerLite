// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using Microsoft.Win32.SafeHandles;

namespace PdfViewerLite.Core.Signatures;

/// <summary>
/// Checks detached CMS (PKCS #7) PDF signatures: that the signed bytes match the signature, whether the signature covers
/// the whole file, and whether the signer's certificate chains to a certificate this computer trusts. The trust check
/// looks up revocation online with a short timeout, so the app only checks when the user asks to see the signatures.
/// </summary>
public static class SignatureVerifier
{
    /// <summary>The DER tag of a SEQUENCE, which a CMS blob starts with.</summary>
    private const byte SequenceTag = 0x30;

    /// <summary>The DER flag marking a long-form length.</summary>
    private const byte LongLengthFlag = 0x80;

    /// <summary>The most length bytes accepted.</summary>
    private const int MaxLengthBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>A byte range holds offset and length pairs.</summary>
    private const int PairSize = 2;

    /// <summary>How long a revocation lookup may take.</summary>
    private static readonly TimeSpan RevocationTimeout = TimeSpan.FromSeconds(5);

    /// <summary>Checks a signature against the file it was read from.</summary>
    /// <param name="signature">The signature as stored.</param>
    /// <param name="filePath">The document's file.</param>
    /// <param name="extraTrust">Certificates to trust in addition to the system's, for example in tests.</param>
    /// <returns>The checked signature.</returns>
    public static DocumentSignature Verify(RawSignature signature, string filePath, X509Certificate2Collection extraTrust)
    {
        ArgumentNullException.ThrowIfNull(signature);
        ArgumentNullException.ThrowIfNull(extraTrust);
        using var file = File.OpenHandle(filePath);
        var length = RandomAccess.GetLength(file);
        if (!TryGetSignedLength(signature.ByteRange, length, out var signedLength) || !TryGetEncodedLength(signature.Contents, out var encodedLength))
        {
            return Unchecked(signature, "The signature's byte range or encoding is damaged.");
        }

        var signed = ReadSigned(file, signature.ByteRange, signedLength);
        var cms = new SignedCms(new(signed), true);
        try
        {
            cms.Decode(signature.Contents.AsSpan(0, encodedLength));
        }
        catch (CryptographicException ex)
        {
            return Unchecked(signature, ex.Message);
        }

        if (cms.SignerInfos.Count == 0 || cms.SignerInfos[0].Certificate is not { } certificate)
        {
            return Unchecked(signature, "The signature does not include the signer's certificate.");
        }

        var intact = CheckSignature(cms);
        var coversAll = signature.ByteRange[^PairSize] + signature.ByteRange[^1] == length;
        var integrity = SignatureIntegrity.Invalid;
        if (intact)
        {
            integrity = coversAll ? SignatureIntegrity.Intact : SignatureIntegrity.ChangedAfterSigning;
        }

        var trusted = IsTrusted(certificate, cms.Certificates, extraTrust, out var detail);
        return new(
            signature.Index,
            certificate.GetNameInfo(X509NameType.SimpleName, false),
            certificate.GetNameInfo(X509NameType.SimpleName, true),
            GetSigningTime(cms.SignerInfos[0]) ?? signature.SigningTime,
            signature.Reason,
            integrity,
            trusted,
            detail);
    }

    /// <summary>Checks the signature mathematically, without judging the certificate.</summary>
    /// <param name="cms">The decoded signature.</param>
    /// <returns><see langword="true"/> when the signed bytes match.</returns>
    private static bool CheckSignature(SignedCms cms)
    {
        try
        {
            cms.CheckSignature(true);
            return true;
        }
        catch (CryptographicException)
        {
            return false;
        }
    }

    /// <summary>Determines whether a certificate chains to a trusted root, offline.</summary>
    /// <param name="certificate">The signer's certificate.</param>
    /// <param name="included">Certificates included in the signature.</param>
    /// <param name="extraTrust">Extra trusted roots.</param>
    /// <param name="detail">Why the chain is not trusted, or an empty string.</param>
    /// <returns><see langword="true"/> when trusted.</returns>
    private static bool IsTrusted(X509Certificate2 certificate, X509Certificate2Collection included, X509Certificate2Collection extraTrust, out string detail)
    {
        using var chain = new X509Chain();
        chain.ChainPolicy.UrlRetrievalTimeout = RevocationTimeout;
        chain.ChainPolicy.ExtraStore.AddRange(included);
        if (extraTrust.Count > 0)
        {
            chain.ChainPolicy.TrustMode = X509ChainTrustMode.CustomRootTrust;
            chain.ChainPolicy.CustomTrustStore.AddRange(extraTrust);
        }

        var trusted = chain.Build(certificate);
        detail = trusted || chain.ChainStatus.Length == 0 ? string.Empty : chain.ChainStatus[0].StatusInformation.Trim();
        return trusted;
    }

    /// <summary>Gets the signing time from the signed attributes.</summary>
    /// <param name="signer">The signer.</param>
    /// <returns>The time, or <see langword="null"/>.</returns>
    private static DateTimeOffset? GetSigningTime(SignerInfo signer)
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

    /// <summary>Validates a byte range against the file and totals its length.</summary>
    /// <param name="range">Offset and length pairs.</param>
    /// <param name="fileLength">The file length.</param>
    /// <param name="signedLength">The total signed length.</param>
    /// <returns><see langword="true"/> when the range lies within the file.</returns>
    private static bool TryGetSignedLength(long[] range, long fileLength, out int signedLength)
    {
        signedLength = 0;
        if (range.Length == 0 || range.Length % PairSize != 0)
        {
            return false;
        }

        long total = 0;
        for (var i = 0; i < range.Length; i += PairSize)
        {
            if (range[i] < 0 || range[i + 1] < 0 || range[i] + range[i + 1] > fileLength)
            {
                return false;
            }

            total += range[i + 1];
        }

        if (total > int.MaxValue)
        {
            return false;
        }

        signedLength = (int)total;
        return true;
    }

    /// <summary>Reads the DER length of a CMS blob, so the zero padding after it is ignored.</summary>
    /// <param name="contents">The padded blob.</param>
    /// <param name="length">The encoded length including tag and length bytes.</param>
    /// <returns><see langword="true"/> when the blob starts with a valid SEQUENCE.</returns>
    private static bool TryGetEncodedLength(byte[] contents, out int length)
    {
        length = 0;
        if (contents.Length < PairSize || contents[0] != SequenceTag)
        {
            return false;
        }

        int first = contents[1];
        if ((first & LongLengthFlag) == 0)
        {
            length = first + PairSize;
            return length <= contents.Length;
        }

        var count = first & ~LongLengthFlag;
        if (count is 0 or > MaxLengthBytes || contents.Length < PairSize + count)
        {
            return false;
        }

        long value = 0;
        for (var i = 0; i < count; i++)
        {
            value = (value << ByteBits) | contents[PairSize + i];
        }

        value += PairSize + count;
        if (value > contents.Length)
        {
            return false;
        }

        length = (int)value;
        return true;
    }

    /// <summary>Reads the signed parts of the file into one buffer.</summary>
    /// <param name="file">The file.</param>
    /// <param name="range">Offset and length pairs.</param>
    /// <param name="signedLength">The total length.</param>
    /// <returns>The signed bytes.</returns>
    private static byte[] ReadSigned(SafeFileHandle file, long[] range, int signedLength)
    {
        var signed = new byte[signedLength];
        var written = 0;
        for (var i = 0; i < range.Length; i += PairSize)
        {
            var part = signed.AsSpan(written, (int)range[i + 1]);
            var offset = range[i];
            while (!part.IsEmpty)
            {
                var read = RandomAccess.Read(file, part, offset);
                if (read == 0)
                {
                    break;
                }

                part = part[read..];
                offset += read;
            }

            written += (int)range[i + 1];
        }

        return signed;
    }

    /// <summary>Creates the result for a signature that could not be checked.</summary>
    /// <param name="signature">The signature.</param>
    /// <param name="detail">Why.</param>
    /// <returns>The result.</returns>
    private static DocumentSignature Unchecked(RawSignature signature, string detail) =>
        new(signature.Index, string.Empty, string.Empty, signature.SigningTime, signature.Reason, SignatureIntegrity.Unknown, false, detail);
}
