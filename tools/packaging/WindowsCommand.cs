// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.IO.Compression;
using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Creates Windows release packages.</summary>
internal static class WindowsCommand
{
    /// <summary>Creates Windows MSI, MSIX and portable ZIP packages.</summary>
    /// <param name="args">The runtime identifier and version.</param>
    /// <returns>The command exit code.</returns>
    /// <exception cref="PlatformNotSupportedException">The command is not running on Windows.</exception>
    /// <exception cref="FileNotFoundException">The published executable is missing.</exception>
    internal static int Run(string[] args)
    {
        if (!OperatingSystem.IsWindows())
        {
            throw new PlatformNotSupportedException("Run Windows packaging on Windows.");
        }

        if (args is not [var rid, var version] || rid is not ("win-x64" or "win-arm64"))
        {
            Console.Error.WriteLine("Usage: PdfViewerLite.Tools package windows <win-x64|win-arm64> <version>");
            return 1;
        }

        var root = Path.GetFullPath(".");
        var artifacts = Path.Combine(root, "artifacts");
        var source = Path.Combine(artifacts, rid);
        if (!File.Exists(Path.Combine(source, "pdfviewerlite.exe")))
        {
            throw new FileNotFoundException("Publish the Windows app before packaging.");
        }

        var staging = Stage(source, artifacts, root, rid, version);
        var package = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.msix");
        MsixWriter.Build(staging, package);
        var msi = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.msi");
        MsiBuilder.Build(source, msi, version, rid, root);
        var portable = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.zip");
        File.Delete(portable);
        ZipFile.CreateFromDirectory(source, portable);
        Console.WriteLine($"Created {package}");
        Console.WriteLine($"Created {msi}");
        Console.WriteLine($"Created {portable}");
        return 0;
    }

    /// <summary>Stages the package payload, manifest and logo assets.</summary>
    /// <param name="source">The published application folder.</param>
    /// <param name="artifacts">The artifacts directory.</param>
    /// <param name="root">The repository root.</param>
    /// <param name="rid">The runtime identifier.</param>
    /// <param name="version">The package version.</param>
    /// <returns>The staging directory.</returns>
    private static string Stage(string source, string artifacts, string root, string rid, string version)
    {
        var staging = Path.Combine(artifacts, $"msix-{rid}");
        if (Directory.Exists(staging))
        {
            Directory.Delete(staging, true);
        }

        foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
        {
            var target = Path.Combine(staging, Path.GetRelativePath(source, file));
            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            File.Copy(file, target);
        }

        File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(staging, "LICENSE"));
        WriteLogos(root, staging);
        var numeric = Version.Parse(version.Split('-', '+')[0]);
        var packageVersion = new Version(numeric.Major, numeric.Minor, numeric.Build, Math.Max(0, numeric.Revision));
        var manifest = XDocument.Load(Path.Combine(root, "packaging/windows/AppxManifest.xml"));
        var identity = manifest.Root!.Element(manifest.Root.Name.Namespace + "Identity")!;
        identity.SetAttributeValue(nameof(Version), packageVersion.ToString());
        identity.SetAttributeValue("ProcessorArchitecture", rid["win-".Length..]);
        manifest.Save(Path.Combine(staging, "AppxManifest.xml"));
        return staging;
    }

    /// <summary>Writes the MSIX logo sizes from the shared source image.</summary>
    /// <param name="root">The repository root.</param>
    /// <param name="staging">The package staging directory.</param>
    private static void WriteLogos(string root, string staging)
    {
        const int smallLogoSize = 44;
        const int tileLogoSize = 150;
        const int storeLogoSize = 50;
        var assets = Path.Combine(staging, "Assets");
        _ = Directory.CreateDirectory(assets);
        var source = Path.Combine(root, "HyperPDFViewerIcon.png");
        IconBuilder.WritePng(source, Path.Combine(assets, "Square44x44Logo.png"), smallLogoSize);
        IconBuilder.WritePng(source, Path.Combine(assets, "Square150x150Logo.png"), tileLogoSize);
        IconBuilder.WritePng(source, Path.Combine(assets, "StoreLogo.png"), storeLogoSize);
    }
}
