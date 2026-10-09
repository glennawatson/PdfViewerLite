// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Security.Cryptography;
using System.Text;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Security;

/// <content>Working out the file key from a password.</content>
public sealed partial class PdfSecurityHandler
{
    /// <summary>The default key length in bytes for revision 2.</summary>
    private const int Revision2KeyLength = 5;

    /// <summary>The number of extra MD5 rounds from revision 3.</summary>
    private const int Md5Rounds = 50;

    /// <summary>The number of RC4 rounds over the user key from revision 3.</summary>
    private const int Rc4Rounds = 20;

    /// <summary>The bits of a 40-bit key.</summary>
    private const int DefaultKeyBits = 40;

    /// <summary>The longest file id kept on the stack.</summary>
    private const int MaxStackId = 64;

    /// <summary>The longest password byte count used from revision 5.</summary>
    private const int MaxUtf8Password = 127;

    /// <summary>The offset of the validation salt in /U and /O from revision 5.</summary>
    private const int ValidationSaltOffset = 32;

    /// <summary>The offset of the key salt in /U and /O from revision 5.</summary>
    private const int KeySaltOffset = 40;

    /// <summary>The length of a salt.</summary>
    private const int SaltLength = 8;

    /// <summary>The length of /U that is mixed into owner hashes from revision 5.</summary>
    private const int UserDataLength = 48;

    /// <summary>The longest input of the revision 5 and 6 hash: password, salt and user data.</summary>
    private const int MaxHashInput = MaxUtf8Password + SaltLength + UserDataLength;

    /// <summary>The longest hash the revision 6 hash produces (SHA-512).</summary>
    private const int MaxHashLength = 64;

    /// <summary>The number of rounds the revision 6 hash always runs.</summary>
    private const int HashMinRounds = 64;

    /// <summary>The offset subtracted from the round count in the revision 6 hash's exit test.</summary>
    private const int HashExitOffset = 32;

    /// <summary>The number of repetitions of the revision 6 hash input.</summary>
    private const int HashRepeats = 64;

    /// <summary>The modulus choosing the revision 6 hash function.</summary>
    private const int HashChoices = 3;

    /// <summary>The revision 6 hash choice that means SHA-384.</summary>
    private const int Sha384Choice = 1;

    /// <summary>Authenticates a revision 2 to 4 file, trying the user and then the owner password.</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="version">The /V value.</param>
    /// <param name="keyLength">The validated key length in bytes.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="password">The password.</param>
    /// <returns>The file key, or <see langword="null"/> when the password is wrong.</returns>
    private static byte[]? Authenticate128(PdfDictionary encrypt, int revision, int version, int keyLength, ReadOnlySpan<byte> firstId, string? password)
    {
        var owner = encrypt.GetStringBytes(KnownName.O);
        var user = encrypt.GetStringBytes(KnownName.U);
        var encryptMetadata = encrypt.GetBoolean(KnownName.EncryptMetadata, true) || version < Revision4;
        var passwordBytes = EncodeLegacyPassword(password);
        var parameters = new KeyParameters(owner, ReadPermissions(encrypt), firstId, revision, keyLength, encryptMetadata);

        var key = ComputeKey(passwordBytes, parameters);
        if (CheckUserKey(key, user, firstId, revision))
        {
            return key;
        }

        // As the owner password, it decrypts /O into the user password.
        var userPassword = RecoverUserPassword(passwordBytes, owner, revision, keyLength);
        key = ComputeKey(userPassword, parameters);
        return CheckUserKey(key, user, firstId, revision) ? key : null;
    }

    /// <summary>Encodes a revision 2 to 4 password as PDFDocEncoding, or as Latin-1 when a character has no PDFDocEncoding byte.</summary>
    /// <param name="password">The password.</param>
    /// <returns>The password bytes.</returns>
    private static byte[] EncodeLegacyPassword(string? password)
    {
        password ??= string.Empty;
        var bytes = new byte[password.Length];
        for (var i = 0; i < password.Length; i++)
        {
            if (!TryEncodeDocChar(password[i], out bytes[i]))
            {
                return Encoding.Latin1.GetBytes(password);
            }
        }

        return bytes;
    }

    /// <summary>Finds the PDFDocEncoding byte of a character.</summary>
    /// <param name="value">The character.</param>
    /// <param name="encoded">The byte.</param>
    /// <returns><see langword="true"/> when the encoding has the character.</returns>
    private static bool TryEncodeDocChar(char value, out byte encoded)
    {
        encoded = 0;
        for (var candidate = 0; candidate <= byte.MaxValue && encoded == 0; candidate++)
        {
            encoded = PdfText.DecodeDocByte((byte)candidate) == value ? (byte)candidate : (byte)0;
        }

        // Byte zero only encodes the NUL character.
        return encoded != 0 || value == '\0';
    }

    /// <summary>Applies the NFKC form of SASLprep to a revision 5 or 6 password; text that cannot be normalised is used as it is.</summary>
    /// <param name="password">The password.</param>
    /// <returns>The normalised password.</returns>
    private static string NormalizePassword(string? password)
    {
        password ??= string.Empty;
        try
        {
            return password.Normalize(NormalizationForm.FormKC);
        }
        catch (ArgumentException)
        {
            return password;
        }
    }

    /// <summary>Computes a file key from a user password (algorithm 2).</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="parameters">The values from the /Encrypt dictionary.</param>
    /// <returns>The key.</returns>
    private static byte[] ComputeKey(ReadOnlySpan<byte> password, in KeyParameters parameters)
    {
        var input = new byte[PaddedLength + parameters.Owner.Length + sizeof(int) + parameters.FirstId.Length + sizeof(int)];
        var span = input.AsSpan();
        PadPassword(password, span[..PaddedLength]);
        var offset = PaddedLength;
        parameters.Owner.CopyTo(span[offset..]);
        offset += parameters.Owner.Length;
        BinaryPrimitives.WriteInt32LittleEndian(span[offset..], parameters.Permissions);
        offset += sizeof(int);
        parameters.FirstId.CopyTo(span[offset..]);
        offset += parameters.FirstId.Length;
        if (parameters.Revision >= Revision4 && !parameters.EncryptMetadata)
        {
            BinaryPrimitives.WriteInt32LittleEndian(span[offset..], -1);
            offset += sizeof(int);
        }

        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(span[..offset], digest);
        if (parameters.Revision > Revision2)
        {
            for (var i = 0; i < Md5Rounds; i++)
            {
                _ = Md5.HashData(digest[..parameters.KeyLength], digest);
            }
        }

        return digest[..parameters.KeyLength].ToArray();
    }

    /// <summary>Checks a file key against /U (algorithms 4 and 5), in constant time.</summary>
    /// <param name="key">The candidate key.</param>
    /// <param name="user">The /U value.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="revision">The revision.</param>
    /// <returns><see langword="true"/> when the key is right.</returns>
    private static bool CheckUserKey(byte[] key, ReadOnlySpan<byte> user, ReadOnlySpan<byte> firstId, int revision)
    {
        if (revision == Revision2)
        {
            Span<byte> check = stackalloc byte[PaddedLength];
            Padding.CopyTo(check);
            Rc4.Apply(key, check);
            return user.Length >= PaddedLength && CryptographicOperations.FixedTimeEquals(user[..PaddedLength], check);
        }

        Span<byte> input = firstId.Length <= MaxStackId ? stackalloc byte[PaddedLength + MaxStackId] : new byte[PaddedLength + firstId.Length];
        Padding.CopyTo(input);
        firstId.CopyTo(input[PaddedLength..]);
        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(input[..(PaddedLength + firstId.Length)], digest);
        Rc4.Apply(key, digest);
        ApplyRoundKeys(key, digest, 1, Rc4Rounds - 1);
        return user.Length >= Md5.HashLength && CryptographicOperations.FixedTimeEquals(user[..Md5.HashLength], digest);
    }

    /// <summary>Decrypts /O with the owner password, giving the padded user password (algorithm 7).</summary>
    /// <param name="ownerPassword">The owner password bytes.</param>
    /// <param name="owner">The /O value.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="keyLength">The key length.</param>
    /// <returns>The user password bytes.</returns>
    private static byte[] RecoverUserPassword(ReadOnlySpan<byte> ownerPassword, ReadOnlySpan<byte> owner, int revision, int keyLength)
    {
        Span<byte> padded = stackalloc byte[PaddedLength];
        PadPassword(ownerPassword, padded);
        Span<byte> digest = stackalloc byte[Md5.HashLength];
        _ = Md5.HashData(padded, digest);
        if (revision > Revision2)
        {
            for (var i = 0; i < Md5Rounds; i++)
            {
                _ = Md5.HashData(digest, digest);
            }
        }

        var key = digest[..keyLength];
        var result = owner[..Math.Min(owner.Length, PaddedLength)].ToArray();
        if (revision == Revision2)
        {
            Rc4.Apply(key, result);
            return result;
        }

        ApplyRoundKeys(key, result, Rc4Rounds - 1, 0);
        return result;
    }

    /// <summary>Applies RC4 with the key XORed with each round number, counting from one round to another.</summary>
    /// <param name="key">The key.</param>
    /// <param name="data">The data.</param>
    /// <param name="from">The first round.</param>
    /// <param name="to">The last round.</param>
    private static void ApplyRoundKeys(ReadOnlySpan<byte> key, Span<byte> data, int from, int to)
    {
        Span<byte> roundKey = stackalloc byte[Md5.HashLength];
        var step = from <= to ? 1 : -1;
        for (var round = from; round != to + step; round += step)
        {
            for (var k = 0; k < key.Length; k++)
            {
                roundKey[k] = (byte)(key[k] ^ round);
            }

            Rc4.Apply(roundKey[..key.Length], data);
        }
    }

    /// <summary>Authenticates a revision 5 or 6 file (algorithms 2.A and 2.B).</summary>
    /// <param name="encrypt">The /Encrypt dictionary.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="password">The password.</param>
    /// <returns>The file key, or <see langword="null"/> when the password is wrong.</returns>
    private static byte[]? Authenticate256(PdfDictionary encrypt, int revision, string? password)
    {
        var owner = encrypt.GetStringBytes(KnownName.O);
        var user = encrypt.GetStringBytes(KnownName.U);
        if (owner.Length < UserDataLength || user.Length < UserDataLength)
        {
            return null;
        }

        var passwordBytes = Encoding.UTF8.GetBytes(NormalizePassword(password));
        var bytes = passwordBytes.AsSpan(0, Math.Min(passwordBytes.Length, MaxUtf8Password));
        Span<byte> digest = stackalloc byte[Sha256Length];

        Hash256(bytes, owner.Slice(ValidationSaltOffset, SaltLength), user[..UserDataLength], revision, digest);
        if (CryptographicOperations.FixedTimeEquals(digest, owner[..Sha256Length]))
        {
            Hash256(bytes, owner.Slice(KeySaltOffset, SaltLength), user[..UserDataLength], revision, digest);
            return UnwrapKey(digest, encrypt.GetStringBytes(KnownName.OE));
        }

        Hash256(bytes, user.Slice(ValidationSaltOffset, SaltLength), [], revision, digest);
        if (!CryptographicOperations.FixedTimeEquals(digest, user[..Sha256Length]))
        {
            return null;
        }

        Hash256(bytes, user.Slice(KeySaltOffset, SaltLength), [], revision, digest);
        return UnwrapKey(digest, encrypt.GetStringBytes(KnownName.UE));
    }

    /// <summary>Decrypts /OE or /UE with an intermediate key, giving the file key.</summary>
    /// <param name="intermediate">The intermediate key.</param>
    /// <param name="wrapped">The wrapped key.</param>
    /// <returns>The file key, or <see langword="null"/> when malformed.</returns>
    private static byte[]? UnwrapKey(ReadOnlySpan<byte> intermediate, ReadOnlySpan<byte> wrapped)
    {
        if (wrapped.Length < Sha256Length)
        {
            return null;
        }

        using var aes = Aes.Create();
        aes.Key = intermediate.ToArray();
        return aes.DecryptCbc(wrapped[..Sha256Length], stackalloc byte[AesBlock], PaddingMode.None);
    }

    /// <summary>The password hash of revisions 5 and 6.</summary>
    /// <param name="password">The password bytes, at most 127.</param>
    /// <param name="salt">The salt.</param>
    /// <param name="userData">The first 48 bytes of /U for owner checks, or empty.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="destination">The 32-byte hash.</param>
    private static void Hash256(ReadOnlySpan<byte> password, ReadOnlySpan<byte> salt, ReadOnlySpan<byte> userData, int revision, Span<byte> destination)
    {
        Span<byte> input = stackalloc byte[MaxHashInput];
        password.CopyTo(input);
        salt.CopyTo(input[password.Length..]);
        userData.CopyTo(input[(password.Length + salt.Length)..]);
        _ = SHA256.HashData(input[..(password.Length + salt.Length + userData.Length)], destination);
        if (revision == Revision6)
        {
            HardenedHash(password, userData, destination);
        }
    }

    /// <summary>The iterated hash that revision 6 adds (algorithm 2.B); one pooled buffer serves every round.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="userData">The first 48 bytes of /U for owner checks, or empty.</param>
    /// <param name="digest">The hash so far, replaced by the result.</param>
    private static void HardenedHash(ReadOnlySpan<byte> password, ReadOnlySpan<byte> userData, Span<byte> digest)
    {
        Span<byte> key = stackalloc byte[MaxHashLength];
        digest.CopyTo(key);
        var keyLength = Sha256Length;
        var block = ArrayPool<byte>.Shared.Rent((MaxUtf8Password + MaxHashLength + UserDataLength) * HashRepeats);
        var encrypted = ArrayPool<byte>.Shared.Rent(block.Length);
        try
        {
            using var aes = Aes.Create();
            for (var round = 0;; round++)
            {
                var length = FillRepeats(password, key[..keyLength], userData, block);
                aes.Key = key[..AesBlock].ToArray();
                _ = aes.EncryptCbc(block.AsSpan(0, length), key.Slice(AesBlock, AesBlock), encrypted, PaddingMode.None);
                keyLength = NextHash(encrypted.AsSpan(0, length), key);
                if (round >= HashMinRounds - 1 && encrypted[length - 1] <= round + 1 - HashExitOffset)
                {
                    break;
                }
            }
        }
        finally
        {
            ArrayPool<byte>.Shared.Return(block);
            ArrayPool<byte>.Shared.Return(encrypted);
        }

        key[..Sha256Length].CopyTo(digest);
    }

    /// <summary>Writes password, key and user data 64 times over.</summary>
    /// <param name="password">The password bytes.</param>
    /// <param name="key">The current key.</param>
    /// <param name="userData">The user data, or empty.</param>
    /// <param name="block">The buffer.</param>
    /// <returns>The bytes written.</returns>
    private static int FillRepeats(ReadOnlySpan<byte> password, ReadOnlySpan<byte> key, ReadOnlySpan<byte> userData, Span<byte> block)
    {
        var unit = password.Length + key.Length + userData.Length;
        for (var i = 0; i < HashRepeats; i++)
        {
            var at = block[(i * unit)..];
            password.CopyTo(at);
            key.CopyTo(at[password.Length..]);
            userData.CopyTo(at[(password.Length + key.Length)..]);
        }

        return unit * HashRepeats;
    }

    /// <summary>Hashes a round's output with SHA-256, SHA-384 or SHA-512, chosen by its first 16 bytes.</summary>
    /// <param name="encrypted">The round's output.</param>
    /// <param name="key">The buffer receiving the hash.</param>
    /// <returns>The hash length.</returns>
    private static int NextHash(ReadOnlySpan<byte> encrypted, Span<byte> key)
    {
        var sum = 0;
        foreach (var b in encrypted[..AesBlock])
        {
            sum += b;
        }

        return (sum % HashChoices) switch
        {
            0 => SHA256.HashData(encrypted, key),
            Sha384Choice => SHA384.HashData(encrypted, key),
            _ => SHA512.HashData(encrypted, key),
        };
    }

    /// <summary>The /Encrypt values that go into a revision 2 to 4 file key.</summary>
    /// <param name="owner">The /O value.</param>
    /// <param name="permissions">The /P value.</param>
    /// <param name="firstId">The first file id.</param>
    /// <param name="revision">The revision.</param>
    /// <param name="keyLength">The key length in bytes.</param>
    /// <param name="encryptMetadata">Whether metadata is encrypted.</param>
    private readonly ref struct KeyParameters(ReadOnlySpan<byte> owner, int permissions, ReadOnlySpan<byte> firstId, int revision, int keyLength, bool encryptMetadata)
    {
        /// <summary>Gets the /O value.</summary>
        public ReadOnlySpan<byte> Owner { get; } = owner;

        /// <summary>Gets the /P value.</summary>
        public int Permissions { get; } = permissions;

        /// <summary>Gets the first file id.</summary>
        public ReadOnlySpan<byte> FirstId { get; } = firstId;

        /// <summary>Gets the revision.</summary>
        public int Revision { get; } = revision;

        /// <summary>Gets the key length in bytes.</summary>
        public int KeyLength { get; } = keyLength;

        /// <summary>Gets a value indicating whether metadata is encrypted.</summary>
        public bool EncryptMetadata { get; } = encryptMetadata;
    }
}
