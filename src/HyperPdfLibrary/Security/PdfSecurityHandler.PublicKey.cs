// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <content>
/// The public-key security handler (<c>/Filter /Adobe.PubSec</c>, ISO 32000-2 7.6.5): the file key comes from a seed
/// that each recipient's CMS EnvelopedData carries, decrypted with the recipient's certificate and private key.
/// </content>
public sealed partial class PdfSecurityHandler
{
    /// <summary>The length of the seed at the start of the enveloped content.</summary>
    private const int SeedLength = 20;

    /// <summary>The length of the permission flags after the seed.</summary>
    private const int PermissionsLength = 4;

    /// <summary>The most recipients read.</summary>
    private const int MaxRecipients = 256;

    /// <summary>The longest recipient (CMS EnvelopedData) accepted, in bytes.</summary>
    private const int MaxRecipientLength = 1 << 20;

    /// <summary>The marker appended to the key input when metadata is not encrypted.</summary>
    private const uint MetadataNotEncrypted = 0xFFFFFFFF;

    /// <summary>
    /// Creates the handler for any supported security handler: the standard handler with a password, or the public-key
    /// handler with a certificate.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="firstId">The first part of the trailer /ID.</param>
    /// <param name="password">The password for the standard handler, or <see langword="null"/>.</param>
    /// <param name="certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreate(PdfDictionary encrypt, ReadOnlySpan<byte> firstId, string? password, X509Certificate2? certificate, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        return IsPublicKey(encrypt) ? TryCreatePublicKey(encrypt, certificate, out handler) : TryCreate(encrypt, firstId, password, out handler);
    }

    /// <summary>
    /// Determines whether an /Encrypt dictionary uses the public-key handler: a handler other than the standard one whose
    /// dictionary or default crypt filter lists /Recipients, as <c>Adobe.PubSec</c> and compatible handlers do.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns><see langword="true"/> for a public-key handler.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static bool IsPublicKey(PdfDictionary encrypt)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        if (encrypt.IsName(KnownName.Filter, KnownName.Standard))
        {
            return false;
        }

        var filter = encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF));
        return encrypt.ContainsKey(KnownName.Recipients) || filter?.ContainsKey(KnownName.Recipients) == true;
    }

    /// <summary>Creates the public-key handler (sub-filters <c>adbe.pkcs7.s3</c>, <c>s4</c> and <c>s5</c>).</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="certificate">A recipient's certificate with its RSA private key, or <see langword="null"/>.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>
    /// Success; <see cref="PdfSecurityResult.CertificateRequired"/> when no certificate is given or none of the recipients is
    /// for it; <see cref="PdfSecurityResult.Damaged"/> when /Recipients is missing or too large.
    /// </returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreatePublicKey(PdfDictionary encrypt, X509Certificate2? certificate, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        handler = null;
        var version = encrypt.GetInt32(KnownName.V);
        var methods = ReadMethods(encrypt, version);
        var filter = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF)) : null;
        var keyLength = PublicKeyLength(encrypt, filter, version, methods);
        if (keyLength == 0)
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        if (ReadRecipients(encrypt, filter) is not { } recipients)
        {
            return PdfSecurityResult.Damaged;
        }

        var envelope = certificate is null ? null : DecryptEnvelope(recipients, certificate);
        if (envelope is not { Length: >= SeedLength + PermissionsLength })
        {
            return PdfSecurityResult.CertificateRequired;
        }

        var encryptMetadata = (filter ?? encrypt).GetBoolean(KnownName.EncryptMetadata, true);
        var key = DerivePublicKey(envelope.AsSpan(0, SeedLength), recipients, encryptMetadata || version < Revision4, keyLength);
        var permissions = BinaryPrimitives.ReadInt32BigEndian(envelope.AsSpan(SeedLength));
        handler = new(key, encrypt.GetInt32(KnownName.R, version), methods, permissions, encryptMetadata) { CryptFilters = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF) : null };
        return PdfSecurityResult.Success;
    }

    /// <summary>Gets the file key length in bytes: 32 for AES-256, else from /Length, checked against the ciphers.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <param name="version">The /V value.</param>
    /// <param name="methods">The string and stream methods.</param>
    /// <returns>The length, or zero when the ciphers do not support it.</returns>
    private static int PublicKeyLength(PdfDictionary encrypt, PdfDictionary? filter, int version, CryptMethods methods)
    {
        var aes256 = methods.Streams == CryptMethod.Aes256 || methods.Strings == CryptMethod.Aes256;
        var keyLength = aes256 ? Sha256Length : ReadPublicKeyLength(encrypt, filter, version);
        return IsValidKeyLength(methods, keyLength, aes256 ? Revision5 : Revision4) ? keyLength : 0;
    }

    /// <summary>Reads the key length in bytes: the crypt filter's /Length from version 4, else /Length, defaulting to 40 bits or 128 for AES.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The length in bytes, or zero when not valid.</returns>
    private static int ReadPublicKeyLength(PdfDictionary encrypt, PdfDictionary? filter, int version)
    {
        var fallback = version >= Revision4 ? DefaultV4KeyBits : DefaultKeyBits;
        var bits = filter?.GetInt32(KnownName.Length, 0) ?? 0;
        bits = bits > 0 ? bits : encrypt.GetInt32(KnownName.Length, fallback);
        if (bits <= 0)
        {
            return 0;
        }

        // A value under 40 is a length in bytes.
        return (bits < DefaultKeyBits ? bits * ByteBits : bits) / ByteBits;
    }

    /// <summary>
    /// Reads the recipients: the default crypt filter's /Recipients from version 4, else the dictionary's. Each is the
    /// DER of a CMS EnvelopedData.
    /// </summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="filter">The default crypt filter, or <see langword="null"/>.</param>
    /// <returns>The recipients, or <see langword="null"/> when there are none, too many, or one is not a string or is too long.</returns>
    private static byte[][]? ReadRecipients(PdfDictionary encrypt, PdfDictionary? filter)
    {
        var value = filter?.Get(KnownName.Recipients) ?? default;
        value = value.IsNull ? encrypt.Get(KnownName.Recipients) : value;
        if (value.Kind == PdfKind.String)
        {
            return IsRecipient(value) ? [value.AsStringBytes().ToArray()] : null;
        }

        if (value.AsArray() is not { Count: > 0 and <= MaxRecipients } array)
        {
            return null;
        }

        var recipients = new byte[array.Count][];
        for (var i = 0; i < recipients.Length; i++)
        {
            var item = array.Get(i);
            if (!IsRecipient(item))
            {
                return null;
            }

            recipients[i] = item.AsStringBytes().ToArray();
        }

        return recipients;
    }

    /// <summary>Determines whether a value can be a recipient: a non-empty string within the size limit.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when usable.</returns>
    private static bool IsRecipient(PdfValue value) =>
        value.Kind == PdfKind.String && value.AsStringBytes().Length is > 0 and <= MaxRecipientLength;

    /// <summary>Decrypts the enveloped content of the first recipient addressed to a certificate.</summary>
    /// <param name="recipients">The recipients.</param>
    /// <param name="certificate">The certificate with its private key.</param>
    /// <returns>The content (the seed and permissions), or <see langword="null"/> when no recipient can be opened.</returns>
    private static byte[]? DecryptEnvelope(byte[][] recipients, X509Certificate2 certificate)
    {
        using var key = certificate.GetRSAPrivateKey();
        if (key is null)
        {
            return null;
        }

        foreach (var recipient in recipients)
        {
            if (TryDecrypt(recipient, certificate, key) is { } content)
            {
                return content;
            }
        }

        return null;
    }

    /// <summary>Decrypts one recipient's EnvelopedData when one of its recipient infos names the certificate.</summary>
    /// <param name="recipient">The EnvelopedData DER.</param>
    /// <param name="certificate">The certificate.</param>
    /// <param name="key">The certificate's private key.</param>
    /// <returns>The content, or <see langword="null"/>.</returns>
    private static byte[]? TryDecrypt(byte[] recipient, X509Certificate2 certificate, RSA key)
    {
        try
        {
            var envelope = new EnvelopedCms();
            envelope.Decode(recipient);
            foreach (var info in envelope.RecipientInfos)
            {
                if (!info.RecipientIdentifier.MatchesCertificate(certificate))
                {
                    continue;
                }

                envelope.Decrypt(info, key);
                return envelope.ContentInfo.Content;
            }
        }
        catch (CryptographicException)
        {
            // A damaged recipient, or one this key cannot open, is skipped like a recipient for someone else.
        }

        return null;
    }

    /// <summary>
    /// Derives the file key: SHA-1 (SHA-256 for AES-256) over the seed, every recipient's bytes in order, and four 0xFF bytes
    /// when metadata is not encrypted, cut to the key length.
    /// </summary>
    /// <param name="seed">The 20-byte seed.</param>
    /// <param name="recipients">The recipients.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    /// <param name="keyLength">The key length in bytes; 32 means AES-256.</param>
    /// <returns>The file key.</returns>
    private static byte[] DerivePublicKey(ReadOnlySpan<byte> seed, byte[][] recipients, bool encryptMetadata, int keyLength)
    {
        using var hasher = IncrementalHash.CreateHash(keyLength == Sha256Length ? HashAlgorithmName.SHA256 : HashAlgorithmName.SHA1);
        hasher.AppendData(seed);
        foreach (var recipient in recipients)
        {
            hasher.AppendData(recipient);
        }

        if (!encryptMetadata)
        {
            Span<byte> marker = stackalloc byte[PermissionsLength];
            BinaryPrimitives.WriteUInt32BigEndian(marker, MetadataNotEncrypted);
            hasher.AppendData(marker);
        }

        Span<byte> digest = stackalloc byte[Sha256Length];
        var written = hasher.GetHashAndReset(digest);
        return digest[..Math.Min(keyLength, written)].ToArray();
    }
}
