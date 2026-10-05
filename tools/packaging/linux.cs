#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package Refit
#:include BuildTools.cs
#:include IPackagingSourceApi.cs
#:include PayloadKind.cs
#:include PayloadEntry.cs
#:include LinuxPayload.cs
#:include DebBuilder.cs
#:include RpmBuilder.cs
#:include SquashfsWriter.cs
#:include AppImageBuilder.cs

using System.Formats.Tar;
using System.IO.Compression;
using PdfViewerLite.Tools.Packaging;
using Refit;

const string x64Rid = "linux-x64";

if (args is not [var rid, var version] || rid is not (x64Rid or "linux-arm64"))
{
    Console.Error.WriteLine("Usage: dotnet run --file linux.cs -- <linux-x64|linux-arm64> <version>");
    return 1;
}

var isX64 = rid == x64Rid;

var architecture = isX64 ? "x86_64" : "aarch64";

var artifacts = Path.GetFullPath("artifacts");

var source = Path.Combine(artifacts, rid);

var runtime = Path.Combine(artifacts, $"appimage-runtime-{architecture}");

using (var client = new HttpClient { BaseAddress = new("https://github.com") })
{
    var api = RestService.ForGenerated<IPackagingSourceApi>(client);
    var address = new Uri($"https://github.com/AppImage/type2-runtime/releases/download/continuous/runtime-{architecture}");
    using var response = await api.DownloadAsync(address, CancellationToken.None).ConfigureAwait(false);
    _ = response.EnsureSuccessStatusCode();
    using var input = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
    using var output = File.Create(runtime);
    await input.CopyToAsync(output).ConfigureAwait(false);
}

AppImageBuilder.Build(runtime, LinuxPayload.CreateAppDirectory(source), Path.Combine(artifacts, $"PdfViewerLite-{version}-{architecture}.AppImage"));

var installed = LinuxPayload.CreateInstalled(source);

DebBuilder.Build(installed, version, isX64 ? "amd64" : "arm64", Path.Combine(artifacts, $"pdfviewerlite_{DebBuilder.ToDebianVersion(version)}_{(isX64 ? "amd64" : "arm64")}.deb"));

RpmBuilder.Build(installed, version, architecture, Path.Combine(artifacts, $"pdfviewerlite-{RpmBuilder.ToRpmVersion(version)}-{RpmBuilder.Release}.{architecture}.rpm"));

using (var archive = File.Create(Path.Combine(artifacts, $"pdfviewerlite-{version}-{rid}.tar.gz")))
using (var gzip = new GZipStream(archive, CompressionLevel.SmallestSize))
{
    TarFile.CreateFromDirectory(source, gzip, true);
}

return 0;
