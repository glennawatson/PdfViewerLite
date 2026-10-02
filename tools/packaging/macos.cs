#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:include BuildTools.cs

using System.Xml.Linq;
using PdfViewerLite.Tools.Packaging;

if (!OperatingSystem.IsMacOS())
{
    throw new PlatformNotSupportedException("Run macOS packaging on macOS.");
}

if (args is not [var rid, var version] || rid is not ("osx-x64" or "osx-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file macos.cs -- <osx-x64|osx-arm64> <version>");
    return 1;
}

var artifacts = Path.GetFullPath("artifacts");

var source = Path.Combine(artifacts, rid);

var stage = Path.Combine(artifacts, $"macos-{rid}");

var app = Path.Combine(stage, "PdfViewerLite.app");

if (Directory.Exists(stage))
{
    Directory.Delete(stage, true);
}

foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var target = Path.Combine(app, "Contents/MacOS", Path.GetRelativePath(source, file));
    _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(file, target);
    File.SetUnixFileMode(target, File.GetUnixFileMode(file));
}

var resources = Path.Combine(app, "Contents/Resources");

_ = Directory.CreateDirectory(resources);

File.Copy("packaging/macos/PdfViewerLite.icns", Path.Combine(resources, "PdfViewerLite.icns"));

var plist = XDocument.Load("packaging/macos/Info.plist");

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

var identity = Environment.GetEnvironmentVariable("MACOS_SIGNING_IDENTITY");

string[] signing = string.IsNullOrEmpty(identity) ? ["--sign", "-"] : ["--options", "runtime", "--timestamp", "--entitlements", "packaging/macos/PdfViewerLite.entitlements", "--sign", identity];

BuildTools.Run("codesign", ["--force", "--deep", .. signing, app]);

BuildTools.Run("codesign", "--verify", "--deep", "--strict", app);

_ = Directory.CreateSymbolicLink(Path.Combine(stage, "Applications"), "/Applications");

BuildTools.Run("hdiutil", "create", "-volname", "PdfViewerLite", "-srcfolder", stage, "-ov", "-format", "UDZO", Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.dmg"));

BuildTools.Run("ditto", "-c", "-k", "--sequesterRsrc", "--keepParent", app, Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.app.zip"));

return 0;
