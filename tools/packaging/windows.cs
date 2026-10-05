#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package System.Drawing.Common
#:property TargetFramework=net11.0-windows10.0.19041.0
#:property TargetFrameworks=
#:include BuildTools.cs
#:include MsiBuilder.cs

using System.Drawing;
using System.Drawing.Imaging;
using System.IO.Compression;
using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;

if (!OperatingSystem.IsWindows())
{
    throw new PlatformNotSupportedException("Run Windows packaging on Windows.");
}

if (args is not [var rid, var version] || rid is not ("win-x64" or "win-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file windows.cs -- <win-x64|win-arm64> <version>");
    return 1;
}

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

var sdkBin = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "Windows Kits", "10", "bin");

string? packer = null;

var selectedVersion = new Version();

foreach (var directory in Directory.EnumerateDirectories(sdkBin))
{
    if (!Version.TryParse(Path.GetFileName(directory), out var sdkVersion) || sdkVersion <= selectedVersion)
    {
        continue;
    }

    var candidate = Path.Combine(directory, "x64", "MakeAppx.exe");
    if (!File.Exists(candidate))
    {
        continue;
    }

    packer = candidate;
    selectedVersion = sdkVersion;
}

if (packer is null)
{
    throw new FileNotFoundException("MakeAppx.exe was not found in the installed Windows SDK.");
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

BuildTools.Run(packer, "pack", "/d", staging, "/p", package, "/o");

var verification = Path.Combine(artifacts, $"msix-check-{rid}");

if (Directory.Exists(verification))
{
    Directory.Delete(verification, true);
}

BuildTools.Run(packer, "unpack", "/d", verification, "/p", package, "/o");

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
