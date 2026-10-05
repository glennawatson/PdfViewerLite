// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the verify msix command.</summary>
internal static class VerifyMsixCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="InvalidDataException">The input data is invalid.</exception>
    /// <exception cref="InvalidOperationException">A required setting or tool is unavailable.</exception>
    /// <exception cref="FileNotFoundException">A required file is missing.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        const int signatureHeaderLength = 4;
        if (args is not [var package])
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools sign verify-msix <package.msix>");
            return 1;
        }

        var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");
        await using var archive = await ZipFile.OpenReadAsync(package);
        var entry = archive.GetEntry("AppxSignature.p7x") ?? throw new InvalidDataException("The MSIX has no signature.");
        await using var source = await entry.OpenAsync();
        await using var buffer = new MemoryStream();
        await source.CopyToAsync(buffer).ConfigureAwait(false);
        var signature = buffer.ToArray();
        if (!signature.AsSpan().StartsWith("PKCX"u8))
        {
            throw new InvalidDataException("The MSIX signature header is invalid.");
        }

        var cms = new SignedCms();
        cms.Decode(signature.AsSpan(signatureHeaderLength));
        cms.CheckSignature(true);
        if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is not { } certificate)
        {
            throw new InvalidDataException("The MSIX must have one signing certificate.");
        }

        var expectedHash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
        if (!CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256), expectedHash))
        {
            throw new InvalidDataException("The MSIX signing certificate differs from CERTUM_CERT_FINGERPRINT.");
        }

        if (OperatingSystem.IsWindows())
        {
            WindowsSignatureVerifier.Verify(package);
        }

        await Console.Out.WriteLineAsync($"Verified the signature and certificate of {package}.");
        return 0;
    }
}
