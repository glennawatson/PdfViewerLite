// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <summary>
/// The standard security handler: works out the file key from a password and decrypts (and encrypts) strings and
/// streams with RC4, AES-128 or AES-256 (revisions 2 to 6). Hashes use one-shot APIs on stack buffers, and one AES
/// instance is reused for every object.
/// </summary>
[DebuggerDisplay("PdfSecurityHandler: R{Revision} {StreamMethod}")]
public sealed partial class PdfSecurityHandler : IDisposable
{
    /// <summary>The AES block size.</summary>
    internal const int AesBlock = 16;

    /// <summary>The length of a SHA-256 hash and an AES-256 key.</summary>
    private const int Sha256Length = 32;

    /// <summary>The bytes of the object number and generation mixed into an object key.</summary>
    private const int ObjectSaltLength = 5;

    /// <summary>The length of the "sAlT" suffix of AES object keys.</summary>
    private const int AesSaltLength = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The shift of an object number's third byte.</summary>
    private const int ThirdByteShift = 16;

    /// <summary>The position of an object number's third byte in the object salt.</summary>
    private const int ThirdNumberByte = 2;

    /// <summary>The position of the generation in the object salt.</summary>
    private const int GenerationOffset = 3;

    /// <summary>The length of the padded password.</summary>
    private const int PaddedLength = 32;

    /// <summary>Revision 2.</summary>
    private const int Revision2 = 2;

    /// <summary>Version 2, the first that has a variable key length.</summary>
    private const int Version2 = 2;

    /// <summary>The default key length in bits for version 4.</summary>
    private const int DefaultV4KeyBits = 128;

    /// <summary>Revision 4, the last RC4/AES-128 revision.</summary>
    private const int Revision4 = 4;

    /// <summary>Revision 5.</summary>
    private const int Revision5 = 5;

    /// <summary>Revision 6.</summary>
    private const int Revision6 = 6;

    /// <summary>The file key.</summary>
    private readonly byte[] _fileKey;

    /// <summary>The AES instance reused for every object; guarded by <see cref="_gate"/> as it is not thread safe.</summary>
    private readonly Aes _aes = Aes.Create();

    /// <summary>Guards <see cref="_aes"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>1 once disposed; written under <see cref="_gate"/>.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfSecurityHandler"/> class.</summary>
    /// <param name="fileKey">The file key.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="methods">How strings and streams are encrypted.</param>
    /// <param name="permissions">The permission flags.</param>
    /// <param name="encryptMetadata">Whether metadata streams are encrypted.</param>
    private PdfSecurityHandler(byte[] fileKey, int revision, CryptMethods methods, int permissions, bool encryptMetadata)
    {
        _fileKey = fileKey;
        Revision = revision;
        StringMethod = methods.Strings;
        StreamMethod = methods.Streams;
        Permissions = permissions;
        EncryptMetadata = encryptMetadata;
    }

    /// <summary>Gets the security handler revision.</summary>
    public int Revision { get; }

    /// <summary>Gets the permission flags (/P).</summary>
    public int Permissions { get; }

    /// <summary>Gets a value indicating whether metadata streams are encrypted.</summary>
    public bool EncryptMetadata { get; }

    /// <summary>Gets how strings are encrypted.</summary>
    internal CryptMethod StringMethod { get; }

    /// <summary>Gets how streams are encrypted.</summary>
    internal CryptMethod StreamMethod { get; }

    /// <summary>Gets the /CF dictionary of named crypt filters, or <see langword="null"/> before revision 4.</summary>
    internal PdfDictionary? CryptFilters { get; init; }

    /// <summary>Gets the padding mixed into passwords before revision 5.</summary>
    private static ReadOnlySpan<byte> Padding =>
    [
        0x28, 0xBF, 0x4E, 0x5E, 0x4E, 0x75, 0x8A, 0x41, 0x64, 0x00, 0x4E, 0x56, 0xFF, 0xFA, 0x01, 0x08,
        0x2E, 0x2E, 0x00, 0xB6, 0xD0, 0x68, 0x3E, 0x80, 0x2F, 0x0C, 0xA9, 0xFE, 0x64, 0x53, 0x69, 0x7A,
    ];

    /// <summary>Gets the suffix of AES-128 object key input.</summary>
    private static ReadOnlySpan<byte> AesSalt => "sAlT"u8;

    /// <summary>Authenticates with a password and creates the handler.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="firstId">The first part of the trailer /ID.</param>
    /// <param name="password">The password; tried as the user password and then the owner password.</param>
    /// <param name="handler">The handler.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="encrypt"/> is <see langword="null"/>.</exception>
    public static PdfSecurityResult TryCreate(PdfDictionary encrypt, ReadOnlySpan<byte> firstId, string? password, out PdfSecurityHandler? handler)
    {
        ArgumentNullException.ThrowIfNull(encrypt);
        handler = null;
        if (!encrypt.IsName(KnownName.Filter, KnownName.Standard))
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        var version = encrypt.GetInt32(KnownName.V);
        var revision = encrypt.GetInt32(KnownName.R);
        if (revision is < Revision2 or > Revision6)
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        var methods = ReadMethods(encrypt, version);
        var keyLength = revision >= Revision5 ? Sha256Length : ReadKeyLength(encrypt, revision, version);
        if (!IsValidKeyLength(methods, keyLength, revision))
        {
            return PdfSecurityResult.UnsupportedHandler;
        }

        var key = revision >= Revision5
            ? Authenticate256(encrypt, revision, password)
            : Authenticate128(encrypt, revision, version, keyLength, firstId, password);
        if (key is null)
        {
            return PdfSecurityResult.WrongPassword;
        }

        handler = new(key, revision, methods, ReadPermissions(encrypt), encrypt.GetBoolean(KnownName.EncryptMetadata, true))
        {
            CryptFilters = version >= Revision4 ? encrypt.GetDictionary(KnownName.CF) : null,
        };
        return PdfSecurityResult.Success;
    }

    /// <summary>Decrypts a string.</summary>
    /// <param name="id">The object holding the string.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] DecryptString(PdfObjectId id, ReadOnlySpan<byte> data) => Decrypt(StringMethod, id, data);

    /// <summary>Decrypts a stream's data.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] DecryptStream(PdfObjectId id, ReadOnlySpan<byte> data) => Decrypt(StreamMethod, id, data);

    /// <summary>Encrypts a string for writing.</summary>
    /// <param name="id">The object holding the string.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncryptString(PdfObjectId id, ReadOnlySpan<byte> data) => Encrypt(StringMethod, id, data);

    /// <summary>Encrypts a stream's data for writing.</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public byte[] EncryptStream(PdfObjectId id, ReadOnlySpan<byte> data) => Encrypt(StreamMethod, id, data);

    /// <summary>Releases the AES instance once any decryption in flight has finished. Later use throws <see cref="ObjectDisposedException"/>.</summary>
    public void Dispose()
    {
        lock (_gate)
        {
            if (Volatile.Read(ref _disposed) != 0)
            {
                return;
            }

            Volatile.Write(ref _disposed, 1);
            _aes.Dispose();
        }
    }

    /// <summary>Decrypts a stream's data with the method of a named crypt filter (a stream's /Crypt filter with a /Name).</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The plain bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] DecryptStream(PdfObjectId id, ReadOnlySpan<byte> data, PdfName filterName) =>
        Decrypt(FilterMethod(CryptFilters, filterName), id, data);

    /// <summary>Encrypts a stream's data with the method of a named crypt filter (a stream's /Crypt filter with a /Name).</summary>
    /// <param name="id">The stream object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The encrypted bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte[] EncryptStream(PdfObjectId id, ReadOnlySpan<byte> data, PdfName filterName) =>
        Encrypt(FilterMethod(CryptFilters, filterName), id, data);

    /// <summary>Gets the method of a named crypt filter.</summary>
    /// <param name="filterName">The crypt filter's name in /CF.</param>
    /// <returns>The method.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal CryptMethod MethodOf(PdfName filterName) => FilterMethod(CryptFilters, filterName);

    /// <summary>Reads the string and stream methods from /V and the crypt filters.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The methods.</returns>
    private static CryptMethods ReadMethods(PdfDictionary encrypt, int version)
    {
        if (version < Revision4)
        {
            return new(CryptMethod.Rc4, CryptMethod.Rc4);
        }

        var filters = encrypt.GetDictionary(KnownName.CF);
        return new(FilterMethod(filters, encrypt.GetName(KnownName.StrF)), FilterMethod(filters, encrypt.GetName(KnownName.StmF)));
    }

    /// <summary>Drops the last bytes of data when the final byte is a pad length from 1 to 16, as PDFium does.</summary>
    /// <param name="raw">The decrypted bytes with their padding.</param>
    /// <returns>The bytes without the pad.</returns>
    private static byte[] StripPlausiblePad(byte[] raw)
    {
        if (raw.Length == 0)
        {
            return raw;
        }

        var pad = raw[^1];
        return pad is >= 1 and <= AesBlock && pad <= raw.Length ? raw.AsSpan(0, raw.Length - pad).ToArray() : raw;
    }

    /// <summary>Reads /P as its low 32 bits, because writers store it as an unsigned number too.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns>The permission flags.</returns>
    private static int ReadPermissions(PdfDictionary encrypt) => unchecked((int)encrypt.GetInteger(KnownName.P));

    /// <summary>Reads the file key length in bytes for revisions 2 to 4, as PDFium does.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="version">The /V value.</param>
    /// <returns>The length in bytes, or zero when it is not valid.</returns>
    private static int ReadKeyLength(PdfDictionary encrypt, int revision, int version)
    {
        if (revision == Revision2 || version < Version2)
        {
            return Revision2KeyLength;
        }

        var bits = version >= Revision4 ? ReadV4KeyBits(encrypt) : encrypt.GetInt32(KnownName.Length, DefaultKeyBits);
        if (bits <= 0)
        {
            return 0;
        }

        // A value under 40 is a length in bytes.
        return (bits < DefaultKeyBits ? bits * ByteBits : bits) / ByteBits;
    }

    /// <summary>Reads the key length of a version 4 file: the stream filter's own /Length, then /Length, then 128.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <returns>The length, in bits or bytes as the writer chose.</returns>
    private static int ReadV4KeyBits(PdfDictionary encrypt)
    {
        var stream = encrypt.GetDictionary(KnownName.CF)?.GetDictionary(encrypt.GetName(KnownName.StmF));
        var bits = stream?.GetInt32(KnownName.Length, 0) ?? 0;
        return bits == 0 ? encrypt.GetInt32(KnownName.Length, DefaultV4KeyBits) : bits;
    }

    /// <summary>Checks that every cipher in use supports the key length.</summary>
    /// <param name="methods">The string and stream methods.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <param name="revision">The revision.</param>
    /// <returns><see langword="true"/> when the combination can be used.</returns>
    private static bool IsValidKeyLength(CryptMethods methods, int keyLength, int revision) =>
        revision >= Revision5
            ? IsAes256OrNone(methods.Strings) && IsAes256OrNone(methods.Streams)
            : IsValidKeyLength(methods.Strings, keyLength) && IsValidKeyLength(methods.Streams, keyLength);

    /// <summary>Checks one cipher against a key length from revisions 2 to 4, whose key comes from a 16-byte MD5 hash.</summary>
    /// <param name="method">The method.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <returns><see langword="true"/> when the cipher supports the length.</returns>
    private static bool IsValidKeyLength(CryptMethod method, int keyLength) => method switch
    {
        CryptMethod.Rc4 => keyLength is >= Revision2KeyLength and <= Md5.HashLength,
        CryptMethod.Aes128 or CryptMethod.Aes256 => keyLength == AesBlock,
        _ => true,
    };

    /// <summary>Checks that a method is allowed with revisions 5 and 6, which use AES-256 only (PDF 2.0 drops RC4 and AES-128).</summary>
    /// <param name="method">The method.</param>
    /// <returns><see langword="true"/> for AES-256 and for no encryption.</returns>
    private static bool IsAes256OrNone(CryptMethod method) => method is CryptMethod.Aes256 or CryptMethod.None;

    /// <summary>Gets the method of a named crypt filter.</summary>
    /// <param name="filters">The /CF dictionary.</param>
    /// <param name="name">The filter name.</param>
    /// <returns>The method.</returns>
    private static CryptMethod FilterMethod(PdfDictionary? filters, PdfName name)
    {
        if (name.IsNone || name.Is(KnownName.Identity))
        {
            return CryptMethod.None;
        }

        var method = filters?.GetDictionary(name)?.GetName(KnownName.CFM) ?? default;
        return method.ToKnownName() switch
        {
            KnownName.V2 => CryptMethod.Rc4,
            KnownName.AESV2 => CryptMethod.Aes128,
            KnownName.AESV3 => CryptMethod.Aes256,
            _ => CryptMethod.None,
        };
    }

    /// <summary>Pads or truncates a password to 32 bytes.</summary>
    /// <param name="password">The password.</param>
    /// <param name="destination">The 32-byte buffer.</param>
    private static void PadPassword(ReadOnlySpan<byte> password, Span<byte> destination)
    {
        var length = Math.Min(password.Length, destination.Length);
        password[..length].CopyTo(destination);
        Padding[..(destination.Length - length)].CopyTo(destination[length..]);
    }

    /// <summary>Decrypts data.</summary>
    /// <param name="method">The method.</param>
    /// <param name="id">The object.</param>
    /// <param name="data">The encrypted bytes.</param>
    /// <returns>The plain bytes.</returns>
    private byte[] Decrypt(CryptMethod method, PdfObjectId id, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Span<byte> keyBuffer = stackalloc byte[Md5.HashLength];
        if (method == CryptMethod.None)
        {
            return data.ToArray();
        }

        if (method == CryptMethod.Rc4)
        {
            var plain = data.ToArray();
            Rc4.Apply(ObjectKey(id, false, keyBuffer), plain);
            return plain;
        }

        return DecryptAes(method == CryptMethod.Aes256 ? _fileKey : ObjectKey(id, true, keyBuffer), data);
    }

    /// <summary>Encrypts data.</summary>
    /// <param name="method">The method.</param>
    /// <param name="id">The object.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The encrypted bytes.</returns>
    private byte[] Encrypt(CryptMethod method, PdfObjectId id, ReadOnlySpan<byte> data)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        Span<byte> keyBuffer = stackalloc byte[Md5.HashLength];
        if (method == CryptMethod.None)
        {
            return data.ToArray();
        }

        if (method == CryptMethod.Rc4)
        {
            var cipher = data.ToArray();
            Rc4.Apply(ObjectKey(id, false, keyBuffer), cipher);
            return cipher;
        }

        return EncryptAes(method == CryptMethod.Aes256 ? _fileKey : ObjectKey(id, true, keyBuffer), data);
    }

    /// <summary>Decrypts AES-CBC data whose first block is the IV, tolerating bad padding.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The IV followed by the cipher text.</param>
    /// <returns>The plain bytes.</returns>
    private byte[] DecryptAes(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        if (data.Length < AesBlock + AesBlock)
        {
            return [];
        }

        var body = data[AesBlock..];
        body = body[..(body.Length - (body.Length % AesBlock))];
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _aes.Key = key.ToArray();
            try
            {
                return _aes.DecryptCbc(body, data[..AesBlock]);
            }
            catch (CryptographicException)
            {
                // Some writers pad wrongly; keep the bytes, dropping a trailing pad when its length byte is plausible.
                var raw = _aes.DecryptCbc(body, data[..AesBlock], PaddingMode.None);
                return StripPlausiblePad(raw);
            }
        }
    }

    /// <summary>Encrypts with AES-CBC, prefixing a random IV.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The plain bytes.</param>
    /// <returns>The IV followed by the cipher text.</returns>
    private byte[] EncryptAes(ReadOnlySpan<byte> key, ReadOnlySpan<byte> data)
    {
        Span<byte> iv = stackalloc byte[AesBlock];
        RandomNumberGenerator.Fill(iv);
        lock (_gate)
        {
            ObjectDisposedException.ThrowIf(_disposed != 0, this);
            _aes.Key = key.ToArray();
            var cipher = _aes.EncryptCbc(data, iv);
            var result = new byte[AesBlock + cipher.Length];
            iv.CopyTo(result);
            cipher.CopyTo(result.AsSpan(AesBlock));
            return result;
        }
    }

    /// <summary>Computes an object's key (algorithm 1).</summary>
    /// <param name="id">The object.</param>
    /// <param name="aes">Whether the key is for AES-128.</param>
    /// <param name="destination">A 16-byte buffer.</param>
    /// <returns>The key, a slice of the buffer.</returns>
    private Span<byte> ObjectKey(PdfObjectId id, bool aes, Span<byte> destination)
    {
        Span<byte> input = stackalloc byte[Md5.HashLength + ObjectSaltLength + AesSaltLength];
        _fileKey.CopyTo(input);
        var salt = input[_fileKey.Length..];
        salt[0] = (byte)id.Number;
        salt[1] = (byte)(id.Number >> ByteBits);
        salt[ThirdNumberByte] = (byte)(id.Number >> ThirdByteShift);
        BinaryPrimitives.WriteUInt16LittleEndian(salt[GenerationOffset..], (ushort)id.Generation);
        var length = _fileKey.Length + ObjectSaltLength;
        if (aes)
        {
            AesSalt.CopyTo(input[length..]);
            length += AesSaltLength;
        }

        _ = Md5.HashData(input[..length], destination);
        return destination[..Math.Min(_fileKey.Length + ObjectSaltLength, Md5.HashLength)];
    }
}
