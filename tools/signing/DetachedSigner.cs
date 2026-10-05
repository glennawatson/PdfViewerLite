// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection.PortableExecutable;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using System.Security.Cryptography.X509Certificates;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Creates and verifies detached CMS signatures with the connected SimplySign key.</summary>
internal static class DetachedSigner
{
    /// <summary>The fixed WIN_CERTIFICATE header size.</summary>
    private const int CertificateHeaderLength = sizeof(uint) + (2 * sizeof(ushort));

    /// <summary>The WIN_CERTIFICATE type for a PKCS#7 signature.</summary>
    private const ushort PkcsSignedData = 2;

    /// <summary>Signs files through .NET's OpenSSL key provider and CMS APIs.</summary>
    /// <param name="assets">The release files.</param>
    /// <param name="signedExecutable">An independently verified executable signed with the release certificate.</param>
    /// <param name="expectedHash">The signing certificate SHA-256 hash.</param>
    /// <exception cref="PlatformNotSupportedException">The command is not running on Linux.</exception>
    /// <exception cref="InvalidOperationException">The PKCS#11 provider configuration is missing.</exception>
    internal static void Sign(string[] assets, string signedExecutable, byte[] expectedHash)
    {
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Detached release signing requires the Linux SimplySign session.");
        }

        var provider = Environment.GetEnvironmentVariable("PKCS11_PROVIDER") ?? throw new InvalidOperationException("PKCS11_PROVIDER is required.");
        var keyUri = Environment.GetEnvironmentVariable("PKCS11_KEY_URI") ?? "pkcs11:type=private";
        using var certificate = ReadCertificate(signedExecutable, expectedHash);
        using var handle = SafeEvpPKeyHandle.OpenKeyFromProvider(provider, keyUri);
        using var key = new RSAOpenSsl(handle);
        foreach (var asset in assets)
        {
            if (asset.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            var signature = new SignedCms(new ContentInfo(File.ReadAllBytes(asset)), detached: true);
            var signer = new CmsSigner(SubjectIdentifierType.IssuerAndSerialNumber, certificate, key) { DigestAlgorithm = new("2.16.840.1.101.3.4.2.1"), };
            signature.ComputeSignature(signer, silent: true);
            var path = $"{asset}.p7s";
            File.WriteAllBytes(path, signature.Encode());
            Verify(asset, path, expectedHash);
            Console.WriteLine($"Signed and verified {Path.GetFileName(asset)}.");
        }
    }

    /// <summary>Verifies detached file integrity and the pinned signing certificate.</summary>
    /// <param name="asset">The signed file.</param>
    /// <param name="signature">The detached signature.</param>
    /// <param name="expectedHash">The signing certificate SHA-256 hash.</param>
    /// <exception cref="InvalidDataException">The signer differs from the pinned certificate.</exception>
    internal static void Verify(string asset, string signature, byte[] expectedHash)
    {
        var cms = new SignedCms(new ContentInfo(File.ReadAllBytes(asset)), detached: true);
        cms.Decode(File.ReadAllBytes(signature));
        cms.CheckSignature(verifySignatureOnly: true);
        _ = FindSigner(cms, expectedHash);
    }

    /// <summary>Reads the pinned public certificate from an independently verified executable.</summary>
    /// <param name="path">The signed executable.</param>
    /// <param name="expectedHash">The signing certificate SHA-256 hash.</param>
    /// <returns>The public signing certificate.</returns>
    /// <exception cref="InvalidDataException">The executable has no matching Authenticode signer.</exception>
    private static X509Certificate2 ReadCertificate(string path, byte[] expectedHash)
    {
        using var source = File.OpenRead(path);
        using var reader = new PEReader(source, PEStreamOptions.LeaveOpen);
        var directory = reader.PEHeaders.PEHeader?.CertificateTableDirectory ?? throw new InvalidDataException("The executable has no PE header.");
        if (directory.RelativeVirtualAddress == 0 || directory.Size <= CertificateHeaderLength)
        {
            throw new InvalidDataException("The executable has no Authenticode signature.");
        }

        source.Position = directory.RelativeVirtualAddress;
        using var binary = new BinaryReader(source);
        var length = checked((int)binary.ReadUInt32());
        _ = binary.ReadUInt16();
        if (binary.ReadUInt16() != PkcsSignedData || length <= CertificateHeaderLength || length > directory.Size)
        {
            throw new InvalidDataException("The executable has an invalid Authenticode certificate table.");
        }

        var bytes = new byte[length - CertificateHeaderLength];
        source.ReadExactly(bytes);
        var cms = new SignedCms();
        cms.Decode(bytes);
        var certificate = FindSigner(cms, expectedHash);
        return X509CertificateLoader.LoadCertificate(certificate.RawData);
    }

    /// <summary>Finds the single pinned signing certificate.</summary>
    /// <param name="cms">The signed message.</param>
    /// <param name="expectedHash">The certificate SHA-256 hash.</param>
    /// <returns>The pinned certificate.</returns>
    /// <exception cref="InvalidDataException">The message has no single pinned signer.</exception>
    private static X509Certificate2 FindSigner(SignedCms cms, byte[] expectedHash)
    {
        if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is not { } certificate)
        {
            throw new InvalidDataException("The message must have one signing certificate.");
        }

        if (!CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256), expectedHash))
        {
            throw new InvalidDataException("The executable signer differs from CERTUM_CERT_FINGERPRINT.");
        }

        return certificate;
    }
}
