#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package Refit
#:include BuildTools.cs
#:include IPackagingSourceApi.cs

using PdfViewerLite.Tools.Packaging;
using Refit;

if (!OperatingSystem.IsLinux())
{
    throw new PlatformNotSupportedException("Run Linux packaging on Linux.");
}

const string x64Rid = "linux-x64";

if (args is not [var rid, var version] || rid is not (x64Rid or "linux-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file linux.cs -- <linux-x64|linux-arm64> <version>");
    return 1;
}

const string appId = "net.glennwatson.PdfViewerLite";

const UnixFileMode executableMode = UnixFileMode.UserRead | UnixFileMode.UserWrite | UnixFileMode.UserExecute
    | UnixFileMode.GroupRead | UnixFileMode.GroupExecute | UnixFileMode.OtherRead | UnixFileMode.OtherExecute;

var architecture = rid == x64Rid ? "x86_64" : "aarch64";

var artifacts = Path.GetFullPath("artifacts");

var source = Path.Combine(artifacts, rid);

var stage = Path.Combine(artifacts, $"AppDir-{architecture}");

if (Directory.Exists(stage))
{
    Directory.Delete(stage, true);
}

foreach (var file in Directory.EnumerateFiles(source, "*", SearchOption.AllDirectories))
{
    var target = Path.Combine(stage, "usr/bin", Path.GetRelativePath(source, file));
    _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(file, target);
    File.SetUnixFileMode(target, File.GetUnixFileMode(file));
}

(string From, string To)[] resources =
[
    ($"packaging/linux/{appId}.desktop", $"usr/share/applications/{appId}.desktop"),
    ($"packaging/linux/{appId}.desktop", $"{appId}.desktop"),
    ($"packaging/linux/{appId}.metainfo.xml", $"usr/share/metainfo/{appId}.appdata.xml"),
    ($"packaging/linux/icons/{appId}.svg", $"usr/share/icons/hicolor/scalable/apps/{appId}.svg"),
    ($"packaging/linux/icons/{appId}.svg", $"{appId}.svg"),
];

foreach (var (from, to) in resources)
{
    var target = Path.Combine(stage, to);
    _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    File.Copy(from, target);
}

_ = File.CreateSymbolicLink(Path.Combine(stage, "AppRun"), "usr/bin/pdfviewerlite");

_ = File.CreateSymbolicLink(Path.Combine(stage, ".DirIcon"), $"{appId}.svg");

var tool = Path.Combine(artifacts, $"appimagetool-{architecture}.AppImage");

using (var client = new HttpClient { BaseAddress = new("https://github.com") })
{
    var api = RestService.ForGenerated<IPackagingSourceApi>(client);
    var address = new Uri($"https://github.com/AppImage/appimagetool/releases/download/continuous/appimagetool-{architecture}.AppImage");
    using var response = await api.DownloadAsync(address, CancellationToken.None).ConfigureAwait(false);
    _ = response.EnsureSuccessStatusCode();
    using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using var output = File.Create(tool);
    await input.CopyToAsync(output).ConfigureAwait(false);
}

File.SetUnixFileMode(tool, executableMode);

Environment.SetEnvironmentVariable("APPIMAGE_EXTRACT_AND_RUN", "1");

Environment.SetEnvironmentVariable("ARCH", architecture);

BuildTools.Run(tool, "--no-appstream", stage, Path.Combine(artifacts, $"PdfViewerLite-{version}-{architecture}.AppImage"));

Environment.SetEnvironmentVariable("VERSION", version);

Environment.SetEnvironmentVariable("NFPM_ARCH", rid == x64Rid ? "amd64" : "arm64");

Environment.SetEnvironmentVariable("PUBLISH_DIR", source);

Environment.SetEnvironmentVariable("MAINTAINER", "Glenn Watson");

Environment.SetEnvironmentVariable("GOBIN", Path.Combine(artifacts, "packaging-tools"));

BuildTools.Run("go", "install", "github.com/goreleaser/nfpm/v2/cmd/nfpm@latest");

foreach (var packager in new[] { "deb", "rpm" })
{
    BuildTools.Run(Path.Combine(artifacts, "packaging-tools", "nfpm"), "package", "-f", "packaging/linux/nfpm.yaml", "-p", packager, "-t", artifacts);
}

BuildTools.Run("tar", "-C", artifacts, "-czf", Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.tar.gz"), rid);

return 0;
