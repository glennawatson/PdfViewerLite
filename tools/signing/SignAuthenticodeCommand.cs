// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Signs and verifies Windows payloads and installers.</summary>
internal static class SignAuthenticodeCommand
{
    /// <summary>Signs all downloaded release assets.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The command is not running in the Linux signing container.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The command received positional arguments.</exception>
    /// <exception cref="InvalidOperationException">The PKCS#11 module path is missing.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 0);
        if (!OperatingSystem.IsLinux())
        {
            throw new PlatformNotSupportedException("Run release signing in the Linux Certum container.");
        }

        Directory.SetCurrentDirectory(Environment.GetEnvironmentVariable("GITHUB_WORKSPACE")!);
        var (folder, assets, hash) = ReadInputs();
        var provider = Environment.GetEnvironmentVariable("PKCS11_PROVIDER") ?? throw new InvalidOperationException("PKCS11_PROVIDER is required.");
        if (!File.Exists("/usr/bin/osslsigncode") || !File.Exists(provider))
        {
            BuildTools.Run("apt-get", "update");
            BuildTools.Run("apt-get", "install", "--yes", "--no-install-recommends", "osslsigncode", "libengine-pkcs11-openssl");
        }

        var scratch = Path.Combine(Environment.GetEnvironmentVariable("RUNNER_TEMP")!, $"release-signing-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(scratch);
        var payloads = WindowsPayload.Extract(assets, scratch);
        await JsignSigner.SignAsync([.. payloads.Values], scratch).ConfigureAwait(false);
        foreach (var payload in payloads.Values)
        {
            BuildTools.Run("osslsigncode", "verify", "-in", payload, "-require-leaf-hash", $"SHA256:{Convert.ToHexString(hash)}");
        }

        WindowsPayload.Replace(assets, payloads, scratch);
        await JsignSigner.SignAsync(Directory.GetFiles(folder, "*.msi*"), scratch).ConfigureAwait(false);
        var verified = await VerifyAllCommand.RunAsync([folder]).ConfigureAwait(false);
        if (verified != 0)
        {
            return verified;
        }

        using var executables = payloads.Values.GetEnumerator();
        if (!executables.MoveNext())
        {
            throw new InvalidOperationException("No independently verified executable is available for the signing certificate.");
        }

        var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT") ?? throw new InvalidOperationException("GITHUB_OUTPUT is required.");
        await File.AppendAllLinesAsync(output, [$"certificate-source={executables.Current}"]).ConfigureAwait(false);
        await Console.Out.WriteLineAsync("Signed and verified Windows payloads and installers.").ConfigureAwait(false);
        return 0;
    }

    /// <summary>Reads the release files and required certificate pin.</summary>
    /// <returns>The release directory, asset paths and certificate hash.</returns>
    /// <exception cref="InvalidOperationException">A required signing setting is missing.</exception>
    /// <exception cref="FileNotFoundException">No assets were downloaded.</exception>
    /// <exception cref="InvalidDataException">The certificate pin is not a SHA-256 hash.</exception>
    internal static (string Folder, string[] Assets, byte[] Hash) ReadInputs()
    {
        var glob = Environment.GetEnvironmentVariable("PKG_GLOB") ?? throw new InvalidOperationException("PKG_GLOB is required.");
        var folder = Path.GetFullPath(Path.GetDirectoryName(glob)!);
        var assets = Directory.GetFiles(folder, Path.GetFileName(glob));
        if (assets.Length == 0)
        {
            throw new FileNotFoundException("No release assets were downloaded.");
        }

        var fingerprint = Environment.GetEnvironmentVariable("CERTUM_CERT_FINGERPRINT") ?? throw new InvalidOperationException("CERTUM_CERT_FINGERPRINT is required.");
        var hash = Convert.FromHexString(fingerprint.Replace(":", string.Empty, StringComparison.Ordinal));
        if (hash.Length != SHA256.HashSizeInBytes)
        {
            throw new InvalidDataException("CERTUM_CERT_FINGERPRINT must be a SHA-256 certificate fingerprint.");
        }

        return (folder, assets, hash);
    }
}
