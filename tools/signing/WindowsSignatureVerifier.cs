// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Signing;

/// <summary>Runs the installed Windows SDK's independent package signature verifier.</summary>
internal static class WindowsSignatureVerifier
{
    /// <summary>Verifies package integrity, signing trust and timestamps.</summary>
    /// <param name="package">The signed package.</param>
    /// <exception cref="FileNotFoundException">The Windows SDK verifier is unavailable.</exception>
    internal static void Verify(string package)
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
}
