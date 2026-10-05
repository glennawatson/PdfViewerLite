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
    internal static int Run(string[] args)
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
            foreach (var package in packages)
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
