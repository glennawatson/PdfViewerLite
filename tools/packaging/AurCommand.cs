// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Writes the AUR PKGBUILD and .SRCINFO for the release DEBs.</summary>
internal static class AurCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The version, the folder holding both release DEBs and the output folder.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args is not [var version, var folder, var output])
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools package aur <version> <deb-folder> <output-folder>").ConfigureAwait(false);
            return 1;
        }

        var x64 = await HashAsync(Path.Combine(folder, AurPackage.GetDebFileName(version, AurPackage.X64))).ConfigureAwait(false);
        var arm64 = await HashAsync(Path.Combine(folder, AurPackage.GetDebFileName(version, AurPackage.Arm64))).ConfigureAwait(false);
        _ = Directory.CreateDirectory(output);
        var pkgbuild = Path.Combine(output, "PKGBUILD");
        await File.WriteAllTextAsync(pkgbuild, AurPackage.CreatePkgbuild(version, x64, arm64)).ConfigureAwait(false);
        await File.WriteAllTextAsync(Path.Combine(output, ".SRCINFO"), AurPackage.CreateSrcinfo(version, x64, arm64)).ConfigureAwait(false);
        Console.WriteLine($"Created {pkgbuild}");
        return 0;
    }

    /// <summary>Hashes a release DEB.</summary>
    /// <param name="path">The DEB path.</param>
    /// <returns>The lowercase hexadecimal SHA-256.</returns>
    /// <exception cref="FileNotFoundException">The DEB is missing.</exception>
    private static async Task<string> HashAsync(string path)
    {
        if (!File.Exists(path))
        {
            throw new FileNotFoundException("The AUR package needs both release DEBs.", path);
        }

        await using var stream = File.OpenRead(path);
        return Convert.ToHexStringLower(await SHA256.HashDataAsync(stream).ConfigureAwait(false));
    }
}
