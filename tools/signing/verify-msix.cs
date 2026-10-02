#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package System.Security.Cryptography.Pkcs
#:include ../packaging/BuildTools.cs

using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;
using PdfViewerLite.Tools.Packaging;

const int signatureHeaderLength = 4;

if (args is not [var package])
{
    Console.Error.WriteLine("Usage: dotnet run --file verify-msix.cs -- <package.msix>");
    return 1;
}

var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");

using var archive = ZipFile.OpenRead(package);

var entry = archive.GetEntry("AppxSignature.p7x") ?? throw new InvalidDataException("The MSIX has no signature.");

using var source = entry.Open();

using var buffer = new MemoryStream();

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
    var kits = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits/10/bin");
    string? signTool = null;
    var latest = new Version();
    foreach (var directory in Directory.EnumerateDirectories(kits))
    {
        var candidate = Path.Combine(directory, "x64/signtool.exe");
        if (!Version.TryParse(Path.GetFileName(directory), out var version) || version <= latest || !File.Exists(candidate))
        {
            continue;
        }

        signTool = candidate;
        latest = version;
    }

    BuildTools.Run(signTool ?? throw new FileNotFoundException("Install the Windows SDK signing tools."), "verify", "/pa", "/all", "/v", package);
}

Console.WriteLine($"Verified the signature and certificate of {package}.");

return 0;
