#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package System.Security.Cryptography.Pkcs
#:include ../packaging/BuildTools.cs
#:include WindowsPayload.cs

using System.IO.Compression;
using System.Security.Cryptography;
using System.Security.Cryptography.Pkcs;

using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;

if (!OperatingSystem.IsLinux())
{
    throw new PlatformNotSupportedException("Run release signing in the Linux Certum container.");
}

Directory.SetCurrentDirectory(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!);

var glob = Environment.GetEnvironmentVariable("PKG_GLOB") ?? throw new InvalidOperationException("PKG_GLOB is required.");

var folder = Path.GetFullPath(Path.GetDirectoryName(glob)!);

var assets = Directory.GetFiles(folder, Path.GetFileName(glob));

if (assets.Length == 0)
{
    throw new FileNotFoundException("No release assets were downloaded.");
}

var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");

var expectedHash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));

if (expectedHash.Length != SHA256.HashSizeInBytes)
{
    throw new InvalidDataException("CERTUM_CERT_FINGERPRINT must be a SHA-256 certificate fingerprint.");
}

BuildTools.Run("apt-get", "update");

BuildTools.Run("apt-get", "install", "--yes", "--no-install-recommends", "osslsigncode", "msitools", "gcab", "cabextract", "libengine-pkcs11-openssl");

var scratch = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, $"release-signing-{Guid.NewGuid():N}");

_ = Directory.CreateDirectory(scratch);

var payloads = WindowsPayload.Extract(assets, scratch);

SignWithJsign(Path.Combine(scratch, "pe", "*"));

foreach (var payload in payloads.Values)
{
    BuildTools.Run("osslsigncode", "verify", "-in", payload, "-require-leaf-hash", $"SHA256:{Convert.ToHexString(expectedHash)}");
}

WindowsPayload.Replace(assets, payloads, scratch);

SignWithJsign(Path.Combine(folder, "*.msi*"));

BuildTools.Run("dotnet", "run", "--file", "tools/signing/verify-all.cs", "--", folder);

var msix = Array.Find(assets, static asset => asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase)) ?? throw new FileNotFoundException("No MSIX signing certificate is available.");

using var archive = ZipFile.OpenRead(msix);

using var signatureStream = archive.GetEntry("AppxSignature.p7x")!.Open();

using var signature = new MemoryStream();

signatureStream.CopyTo(signature);

var cms = new SignedCms();

const int signatureHeaderLength = 4;

cms.Decode(signature.GetBuffer().AsSpan(signatureHeaderLength, checked((int)signature.Length) - signatureHeaderLength));

if (cms.SignerInfos.Count != 1 || cms.SignerInfos[0].Certificate is not { } certificate
    || !CryptographicOperations.FixedTimeEquals(certificate.GetCertHash(HashAlgorithmName.SHA256), expectedHash))
{
    throw new InvalidDataException("The signing certificate differs from CERTUM_CERT_FINGERPRINT.");
}

var signer = Path.Combine(scratch, "signer.pem");

File.WriteAllText(signer, certificate.ExportCertificatePem());

var chain = Path.Combine(scratch, "chain.pem");

using (var writer = File.CreateText(chain))
{
    foreach (var candidate in cms.Certificates)
    {
        if (!candidate.Equals(certificate))
        {
            writer.WriteLine(candidate.ExportCertificatePem());
        }
    }
}

var module = Environment.GetEnvironmentVariable("SS_PKCS11") ?? throw new InvalidOperationException("SS_PKCS11 is required.");

const string engine = "/usr/lib/x86_64-linux-gnu/engines-3/pkcs11.so";

if (!File.Exists(engine))
{
    throw new FileNotFoundException("The OpenSSL PKCS#11 engine was not installed.", engine);
}

var config = Path.Combine(scratch, "openssl.cnf");

File.WriteAllText(config, $"""
    openssl_conf = openssl_init
    [openssl_init]
    engines = engines
    [engines]
    pkcs11 = pkcs11
    [pkcs11]
    engine_id = pkcs11
    dynamic_path = {engine}
    MODULE_PATH = {module}
    PIN = ""
    init = 0
    """);

Environment.SetEnvironmentVariable("OPENSSL_CONF", config);

foreach (var asset in assets)
{
    if (asset.EndsWith(".msi", StringComparison.OrdinalIgnoreCase) || asset.EndsWith(".msix", StringComparison.OrdinalIgnoreCase))
    {
        continue;
    }

    var detached = $"{asset}.p7s";
    BuildTools.Run("openssl", ["cms", "-sign", "-binary", "-md", "sha256", "-in", asset, "-outform", "DER", "-out", detached,
        "-signer", signer, "-certfile", chain, "-engine", "pkcs11", "-keyform", "engine", "-inkey", "pkcs11:type=private", "-nosmimecap"]);
    BuildTools.Run("openssl", "cms", "-verify", "-binary", "-inform", "DER", "-in", detached, "-content", asset, "-purpose", "any", "-out", "/dev/null");

    var detachedCms = new SignedCms();
    detachedCms.Decode(File.ReadAllBytes(detached));
    if (detachedCms.SignerInfos.Count != 1 || detachedCms.SignerInfos[0].Certificate is not { } detachedCertificate
        || !CryptographicOperations.FixedTimeEquals(detachedCertificate.GetCertHash(HashAlgorithmName.SHA256), expectedHash))
    {
        throw new InvalidDataException($"The signature of {asset} has the wrong certificate.");
    }
}

Console.WriteLine($"Signed and verified all {assets.Length} release assets.");

return 0;

static void SignWithJsign(string glob)
{
    Environment.SetEnvironmentVariable("PKG_GLOB", glob);
    var script = Environment.GetEnvironmentVariable("JSIGN_SCRIPT") ?? throw new InvalidOperationException("JSIGN_SCRIPT is required.");
    BuildTools.Run("dotnet", "run", "--file", script);
}
