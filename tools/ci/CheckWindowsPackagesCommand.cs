// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;
using PdfViewerLite.Tools.VoiceModels;
using Refit;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Checks the managed installer replacement paths with Windows package APIs.</summary>
internal static class CheckWindowsPackagesCommand
{
    /// <summary>The shared asset download client.</summary>
    private static readonly HttpClient Client = new();

    /// <summary>Checks copies of the packaged installers after payload replacement.</summary>
    /// <param name="args">The artifacts directory.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The host is not Windows.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Run installer format checks on Windows.");
        }

        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 1);
        var scratch = Path.Combine(Path.GetTempPath(), $"windows-packages-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(scratch);
        try
        {
            var api = RestService.ForGenerated<IVoiceAssetApi>(Client);
            var path = Path.Combine(scratch, "published.msix");
            Uri address = new("https://github.com/glennawatson/PdfViewerLite/releases/download/1.0.0/pdfviewerlite-1.0.0-win-x64.msix");
            using var response = await api.DownloadAsync(address, CancellationToken.None).ConfigureAwait(false);
            _ = response.EnsureSuccessStatusCode();
            await using (var destination = File.Create(path))
            {
                await response.Content.CopyToAsync(destination).ConfigureAwait(false);
            }

            WindowsPackageValidator.ValidateMsix(path);
            WindowsSignatureVerifier.Verify(path);
            foreach (var package in Directory.GetFiles(args[0], "*.msi"))
            {
                Check(package, scratch);
            }
        }
        finally
        {
            Directory.Delete(scratch, true);
        }

        return 0;
    }

    /// <summary>Replaces installer payloads with copies of the portable executables.</summary>
    /// <param name="package">The MSI path.</param>
    /// <param name="scratch">The temporary directory.</param>
    private static void Check(string package, string scratch)
    {
        var msi = Path.Combine(scratch, Path.GetFileName(package));
        var portable = Path.ChangeExtension(msi, ".zip");
        var msix = Path.ChangeExtension(msi, ".msix");
        File.Copy(package, msi);
        File.Copy(Path.ChangeExtension(package, ".zip"), portable);
        File.Copy(Path.ChangeExtension(package, ".msix"), msix);
        var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
        using (var archive = ZipFile.OpenRead(portable))
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                using var source = entry.Open();
                var hash = Convert.ToHexString(SHA256.HashData(source));
                var target = Path.Combine(scratch, hash);
                if (payloads.TryAdd(hash, target))
                {
                    entry.ExtractToFile(target);
                }
            }
        }

        MsiPayload.Replace(msi, payloads, scratch);
        MsiPayload.Validate(msi);
        WindowsPackageValidator.ValidateMsi(msi);
        MsixWriter.Replace(msix, payloads);
        WindowsPackageValidator.ValidateMsix(msix);
    }
}
