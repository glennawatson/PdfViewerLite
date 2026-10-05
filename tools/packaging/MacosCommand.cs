// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the macos command.</summary>
internal static class MacosCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The current platform is unsupported.</exception>
    internal static int Run(string[] args)
    {
        if (!OperatingSystem.IsMacOS())
        {
            throw new PlatformNotSupportedException("Run macOS packaging on macOS.");
        }

        if (args is not [var rid, var version] || rid is not ("osx-x64" or "osx-arm64"))
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools package macos <osx-x64|osx-arm64> <version>");
            return 1;
        }

        var artifacts = Path.GetFullPath("artifacts");
        var source = Path.Combine(artifacts, rid);

        // Spotlight can hold handles on newly signed files while hdiutil copies the bundle.
        var stage = Path.Combine(artifacts, $"macos-{rid}.noindex");
        var app = Path.Combine(stage, "Hyper PDF Viewer.app");
        if (Directory.Exists(stage))
        {
            Directory.Delete(stage, true);
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            // Data such as the text recognition languages belongs in Resources; Contents/MacOS holds only code to sign.
            var relative = Path.GetRelativePath(source, file);
            var folder = relative.StartsWith("tessdata", StringComparison.Ordinal) ? "Contents/Resources" : "Contents/MacOS";
            var target = Path.Combine(app, folder, relative);
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
            File.SetUnixFileMode(target, File.GetUnixFileMode(file));
        }

        var resources = Path.Combine(app, "Contents/Resources");
        _ = Directory.CreateDirectory(resources);
        File.Copy("packaging/macos/PdfViewerLite.icns", Path.Combine(resources, "PdfViewerLite.icns"));
        WritePlist(app, version);
        SignApplication(app);
        _ = Directory.CreateSymbolicLink(Path.Combine(stage, "Applications"), "/Applications");
        BuildTools.Run(
            "hdiutil",
            "create",
            "-volname",
            "Hyper PDF Viewer",
            "-srcfolder",
            stage,
            "-ov",
            "-fs",
            "HFS+",
            "-nospotlight",
            "-verbose",
            "-format",
            "UDZO",
            Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.dmg"));
        BuildTools.Run("hdiutil", "verify", Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.dmg"));
        BuildTools.Run("ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", app, Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.app.zip"));
        return 0;
    }

    /// <summary>Signs and verifies the macOS application.</summary>
    /// <param name="app">The app.</param>
    private static void SignApplication(string app)
    {
        var identity = Environment.GetEnvironmentVariable("MACOS_SIGNING_IDENTITY");
        string[] signing = string.IsNullOrEmpty(identity)
            ? ["--sign", "-"]
            : ["--options", "runtime", "--timestamp", "--entitlements", "packaging/macos/PdfViewerLite.entitlements", "--sign", identity];
        BuildTools.Run("codesign", ["--force", "--deep", .. signing, app]);
        BuildTools.Run("codesign", "--verify", "--deep", "--strict", app);
    }

    /// <summary>Writes and validates the application property list.</summary>
    /// <param name="app">The application bundle.</param>
    /// <param name="version">The release version.</param>
    private static void WritePlist(string app, string version)
    {
        var plist = XDocument.Load("packaging/macos/Info.plist");
        if (plist.DocumentType is { } documentType)
        {
            // Apple's plist parser rejects the empty internal subset added by XDocument.Load.
            documentType.InternalSubset = null;
        }

        foreach (var element in plist.Descendants("string"))
        {
            if (element.Value == "@VERSION@")
            {
                element.Value = version.Split('-', '+')[0];
            }
        }

        var plistPath = Path.Combine(app, "Contents/Info.plist");
        plist.Save(plistPath);
        BuildTools.Run("plutil", "-lint", plistPath);
    }
}
