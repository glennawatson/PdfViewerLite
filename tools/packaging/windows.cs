#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package System.Drawing.Common
#:package Refit
#:property TargetFramework=net11.0-windows10.0.19041.0
#:property TargetFrameworks=
#:include BuildTools.cs
#:include IPackagingSourceApi.cs
#:include MsiBuilder.cs

using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;
using Refit;

if (!OperatingSystem.IsWindows())
{
    throw new PlatformNotSupportedException("Run Windows packaging on Windows.");
}

if (args is not [var rid, var version] || rid is not ("win-x64" or "win-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file windows.cs -- <win-x64|win-arm64> <version>");
    return 1;
}

const string sdkRevision = "25a65f5c1690930813bcc10cdf1d59fa865f2bb1";

const int smallLogoSize = 44;

const int tileLogoSize = 150;

const int storeLogoSize = 50;

var root = Path.GetFullPath(".");

var artifacts = Path.Combine(root, "artifacts");

var source = Path.Combine(artifacts, rid);

if (!File.Exists(Path.Combine(source, "pdfviewerlite.exe")))
{
    throw new FileNotFoundException("Publish the Windows app before packaging.");
}

var numeric = Version.Parse(version.Split('-', '+')[0]);

var packageVersion = new Version(numeric.Major, numeric.Minor, numeric.Build, Math.Max(0, numeric.Revision));

var sdkRoot = Path.Combine(artifacts, "msix-sdk");

var sdkSource = Path.Combine(sdkRoot, "source");

var sdkBuild = Path.Combine(sdkRoot, "build");

var packer = Path.Combine(sdkBuild, "bin", "Release", "makemsix.exe");

if (!File.Exists(packer))
{
    if (!Directory.Exists(sdkSource))
    {
        _ = Directory.CreateDirectory(sdkRoot);
        using var client = new HttpClient { BaseAddress = new("https://codeload.github.com") };
        var api = RestService.ForGenerated<IPackagingSourceApi>(client);
        using var response = await api.DownloadAsync(new($"https://codeload.github.com/microsoft/msix-packaging/zip/{sdkRevision}"), CancellationToken.None).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        var archivePath = Path.Combine(sdkRoot, "source.zip");
        using (var stream = await response.Content.ReadAsStreamAsync().ConfigureAwait(false))
        using (var destination = File.Create(archivePath))
        {
            await stream.CopyToAsync(destination).ConfigureAwait(false);
        }

        ZipFile.ExtractToDirectory(archivePath, sdkRoot, true);
        Directory.Move(Path.Combine(sdkRoot, $"msix-packaging-{sdkRevision}"), sdkSource);
        File.Delete(archivePath);
    }

    BuildTools.Run(
        "cmake",
        "-S",
        sdkSource,
        "-B",
        sdkBuild,
        "-A",
        "x64",
        "-DWIN32=on",
        "-DMSIX_PACK=on",
        "-DUSE_VALIDATION_PARSER=on",
        "-DUSE_STATIC_MSVC=on",
        "-DMSIX_TESTS=off",
        "-DMSIX_SAMPLES=off");
    BuildTools.Run("cmake", "--build", sdkBuild, "--config", "Release", "--target", "makemsix", "--parallel");
}

var staging = Path.Combine(artifacts, $"msix-{rid}");

if (Directory.Exists(staging))
{
    Directory.Delete(staging, true);
}

_ = Directory.CreateDirectory(staging);

foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var target = Path.Combine(staging, Path.GetRelativePath(source, file));
    _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(file, target);
}

File.Copy(Path.Combine(root, "LICENSE"), Path.Combine(staging, "LICENSE"));

var assets = Path.Combine(staging, "Assets");

_ = Directory.CreateDirectory(assets);

using (var icon = new Icon(Path.Combine(root, "packaging/windows/PdfViewerLite.ico")))
using (var image = icon.ToBitmap())
{
    foreach (var (name, size) in new[] { ("Square44x44Logo.png", smallLogoSize), ("Square150x150Logo.png", tileLogoSize), ("StoreLogo.png", storeLogoSize) })
    {
        using var resized = new Bitmap(image, size, size);
        resized.Save(Path.Combine(assets, name), ImageFormat.Png);
    }
}

var manifest = XDocument.Load(Path.Combine(root, "packaging/windows/AppxManifest.xml"));

var identity = manifest.Root!.Element(manifest.Root.Name.Namespace + "Identity")!;

identity.SetAttributeValue(nameof(Version), packageVersion.ToString());

identity.SetAttributeValue("ProcessorArchitecture", rid["win-".Length..]);

manifest.Save(Path.Combine(staging, "AppxManifest.xml"));

var package = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.msix");

File.Delete(package);

BuildTools.Run(packer, "pack", "-d", staging, "-p", package);

var verification = Path.Combine(artifacts, $"msix-check-{rid}");

if (Directory.Exists(verification))
{
    Directory.Delete(verification, true);
}

BuildTools.Run(packer, "unpack", "-ss", "-d", verification, "-p", package);

using (var archive = ZipFile.OpenRead(package))
{
    if (archive.GetEntry("AppxManifest.xml") is null || archive.GetEntry("pdfviewerlite.exe") is null || archive.GetEntry("AppxBlockMap.xml") is null)
    {
        throw new InvalidDataException("The MSIX package is missing required files.");
    }
}

var msiPackage = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.msi");

File.Delete(msiPackage);

MsiBuilder.Build(source, msiPackage, version, rid, root);

var portable = Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.zip");

File.Delete(portable);

ZipFile.CreateFromDirectory(source, portable);

Console.WriteLine($"Created {package}");

Console.WriteLine($"Created {msiPackage}");

Console.WriteLine($"Created {portable}");

return 0;
