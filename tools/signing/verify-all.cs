#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include ../packaging/BuildTools.cs

using System.Security.Cryptography;

using PdfViewerLite.Tools.Packaging;

if (args is not [var folder])
{
    Console.Error.WriteLine("Usage: dotnet run --file verify-all.cs -- <folder>");
    return 1;
}

var packages = Directory.GetFiles(folder, "*.msi*");

if (packages.Length == 0)
{
    throw new FileNotFoundException("No Windows installers were downloaded.");
}

if (OperatingSystem.IsLinux())
{
    var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");
    var hash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));

    if (hash.Length != SHA256.HashSizeInBytes)
    {
        throw new InvalidDataException("CERTUM_CERT_FINGERPRINT must be a SHA-256 certificate fingerprint.");
    }

    if (!File.Exists("/usr/bin/osslsigncode"))
    {
        BuildTools.Run("apt-get", "update");
        BuildTools.Run("apt-get", "install", "--yes", "--no-install-recommends", "osslsigncode");
    }

    var expectedHash = $"SHA256:{Convert.ToHexString(hash)}";

    foreach (var package in packages)
    {
        BuildTools.Run("osslsigncode", "verify", "-in", package, "-require-leaf-hash", expectedHash);
    }

    return 0;
}

foreach (var package in packages)
{
    var verifier = Path.GetExtension(package).ToLowerInvariant() switch
    {
        ".msix" => "tools/signing/verify-msix.cs",
        ".msi" => "tools/signing/verify-msi.cs",
        _ => throw new InvalidDataException($"Unsupported installer: {package}")
    };

    BuildTools.Run("dotnet", "run", "--file", verifier, "--", package);
}

return 0;
