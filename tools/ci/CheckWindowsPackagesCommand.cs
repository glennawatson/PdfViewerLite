// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;
using PdfViewerLite.Tools.Signing;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Checks the managed installer replacement paths with Windows package APIs.</summary>
internal static class CheckWindowsPackagesCommand
{
    /// <summary>Checks copies of the packaged installers after payload replacement.</summary>
    /// <param name="args">The artifacts directory.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The host is not Windows.</exception>
    /// <exception cref="FileNotFoundException">The artifacts directory has no MSI packages.</exception>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Run installer format checks on Windows.");
        }

        ArgumentOutOfRangeException.ThrowIfNotEqual(args.Length, 1);
        var packages = Directory.GetFiles(args[0], "*.msi");
        if (packages.Length == 0)
        {
            throw new FileNotFoundException("No Windows installers were packaged.");
        }

        var scratch = Path.Combine(Path.GetTempPath(), $"windows-packages-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(scratch);
        try
        {
            await WindowsPackageInstallation.PrepareAsync(scratch).ConfigureAwait(false);
            foreach (var package in packages)
            {
                await CheckAsync(package, scratch).ConfigureAwait(false);
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
    /// <returns>A task.</returns>
    private static async Task CheckAsync(string package, string scratch)
    {
        var msi = Path.Combine(scratch, Path.GetFileName(package));
        var portable = Path.ChangeExtension(msi, ".zip");
        var msix = Path.ChangeExtension(msi, ".msix");
        File.Copy(package, msi);
        File.Copy(Path.ChangeExtension(package, ".zip"), portable);
        File.Copy(Path.ChangeExtension(package, ".msix"), msix);
        WindowsPackageInstallation.SetTestIdentity(msix, scratch);
        var extracted = Path.Combine(scratch, "portable");
        await ZipFile.ExtractToDirectoryAsync(portable, extracted).ConfigureAwait(false);
        var payloads = new Dictionary<string, string>(StringComparer.Ordinal);
        await using (var archive = await ZipFile.OpenReadAsync(portable).ConfigureAwait(false))
        {
            foreach (var entry in archive.Entries)
            {
                if (!entry.FullName.EndsWith(".exe", StringComparison.OrdinalIgnoreCase) && !entry.FullName.EndsWith(".dll", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                await using var source = await entry.OpenAsync().ConfigureAwait(false);
                var hash = Convert.ToHexString(await SHA256.HashDataAsync(source).ConfigureAwait(false));
                var target = Path.Combine(extracted, entry.FullName);
                _ = payloads.TryAdd(hash, target);
            }
        }

        WindowsPackageInstallation.Sign(scratch, payloads.Values);
        MsiPayload.Replace(msi, payloads, scratch);
        MsiPayload.Validate(msi);
        WindowsPackageValidator.ValidateMsi(msi);
        MsixWriter.Replace(msix, payloads);
        WindowsPackageValidator.ValidateMsix(msix);
        WindowsPackageInstallation.Sign(scratch, [msi, msix]);
        var pdf = Path.Combine(scratch, "installation-check.pdf");
        _ = await CreateCheckPdfCommand.RunAsync([pdf]).ConfigureAwait(false);
        await PackageLaunch.CheckAsync(Path.Combine(extracted, "pdfviewerlite.exe"), pdf).ConfigureAwait(false);
#if WINDOWS
        await WindowsPackageInstallation.CheckAsync(msi, msix, scratch, pdf).ConfigureAwait(false);
#endif
    }
}
