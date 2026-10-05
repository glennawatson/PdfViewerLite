// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the verify all command.</summary>
internal static class VerifyAllCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="FileNotFoundException">A required file is missing.</exception>
    /// <exception cref="InvalidDataException">The input data is invalid.</exception>
    /// <exception cref="InvalidOperationException">A required setting or tool is unavailable.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 1);
        var folder = args[0];
        var packages = Directory.GetFiles(folder, "*.msi*");
        if (packages.Length == 0)
        {
            throw new FileNotFoundException("No Windows installers were downloaded.");
        }

        ValidateInstallerMetadata(packages);

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
            var verified = Path.GetExtension(package).ToLowerInvariant() switch
            {
                ".msix" => await VerifyMsixCommand.RunAsync([package]).ConfigureAwait(false),
                ".msi" => VerifyMsiCommand.Run([package]),
                _ => throw new InvalidDataException($"Unsupported installer: {package}")
            };
            if (verified != 0)
            {
                return verified;
            }
        }

        return 0;
    }

    /// <summary>Checks installer storage metadata before verifying signatures.</summary>
    /// <param name="packages">The installer paths.</param>
    private static void ValidateInstallerMetadata(string[] packages)
    {
        foreach (var package in packages)
        {
            if (Path.GetExtension(package).Equals(".msi", StringComparison.OrdinalIgnoreCase))
            {
                MsiPayload.Validate(package);
            }
        }
    }
}
