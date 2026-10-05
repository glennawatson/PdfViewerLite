// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using OpenMcdf;
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the verify msi command.</summary>
internal static class VerifyMsiCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="InvalidDataException">The input data is invalid.</exception>
    /// <exception cref="InvalidOperationException">A required setting or tool is unavailable.</exception>
    /// <exception cref="FileNotFoundException">A required file is missing.</exception>
    internal static int Run(string[] args)
    {
        if (args is not [var package] || !OperatingSystem.IsWindows())
        {
            Console.Error.WriteLine("Run on Windows: PdfViewerLite.Tools sign verify-msi <package.msi>");
            return 1;
        }

        var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");
        using var root = RootStorage.Open(package, FileMode.Open, FileAccess.Read);
        using var signature = root.OpenStream("\u0005DigitalSignature");
        using var content = new MemoryStream();
        signature.CopyTo(content);
        var cms = new SignedCms();
        cms.Decode(content.ToArray());
        cms.CheckSignature(verifySignatureOnly: true);
        if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is not { } certificate)
        {
            throw new InvalidDataException("The MSI must have one signing certificate.");
        }

        var expectedHash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
        if (!CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256), expectedHash))
        {
            throw new InvalidDataException("The MSI signing certificate differs from CERTUM_CERT_FINGERPRINT.");
        }

        WindowsSignatureVerifier.Verify(package);
        Console.WriteLine($"Verified the signature and certificate of {package}.");
        return 0;
    }
}
