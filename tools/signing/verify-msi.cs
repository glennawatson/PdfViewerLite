#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include ../packaging/BuildTools.cs

using System.Diagnostics;
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;

if (args is not [var package] || !OperatingSystem.IsWindows())
{
    Console.Error.WriteLine("Run on Windows: dotnet run --file verify-msi.cs -- <package.msi>");
    return 1;
}

var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");

Environment.SetEnvironmentVariable("MSI_VERIFY_PACKAGE", Path.GetFullPath(package));

const string certificateScript = """
    $ErrorActionPreference = 'Stop'
    $signature = Get-AuthenticodeSignature -LiteralPath $env:MSI_VERIFY_PACKAGE
    if ($signature.Status -ne 'Valid') { throw "Invalid MSI signature: $($signature.Status)" }
    [Convert]::ToHexString([System.Security.Cryptography.SHA256]::HashData($signature.SignerCertificate.RawData))
    """;

var result = Process.RunAndCaptureText("pwsh", ["-NoProfile", "-NonInteractive", "-Command", certificateScript]);

if (result.ExitStatus.ExitCode != 0)
{
    throw new InvalidDataException(result.StandardError);
}

var expectedHash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));

if (!CryptographicOperations.FixedTimeEquals(Convert.FromHexString(result.StandardOutput.Trim()), expectedHash))
{
    throw new InvalidDataException("The MSI signing certificate differs from CERTUM_CERT_FINGERPRINT.");
}

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

Console.WriteLine($"Verified the signature and certificate of {package}.");

return 0;
