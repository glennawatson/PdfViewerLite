#!/usr/bin/env dotnet
// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

#:package Refit
#:include IVoiceAssetApi.cs

using System.Diagnostics;
using System.Net;
using PdfViewerLite.Tools.VoiceModels;
using Refit;

const int argumentCount = 3;

if (args.Length != argumentCount || args[0] is not ("check" or "publish"))
{
    Console.Error.WriteLine("Usage: dotnet run --file release.cs -- check <tag> <publish: true|false> | publish <tag> <folder>");
    return 1;
}

var repository = Environment.GetEnvironmentVariable("GITHUB_REPOSITORY") ?? throw new InvalidOperationException("GITHUB_REPOSITORY is required.");

var tag = args[1];

if (args[0] == "check")
{
    using var client = new HttpClient { BaseAddress = new("https://api.github.com") };
    client.DefaultRequestHeaders.Authorization = new("Bearer", Environment.GetEnvironmentVariable("GH_TOKEN"));
    var api = RestService.ForGenerated<IVoiceAssetApi>(client);
    using var response = await api.DownloadAsync(new($"https://api.github.com/repos/{repository}/releases/tags/{Uri.EscapeDataString(tag)}"), CancellationToken.None).ConfigureAwait(false);
    if (response.StatusCode != HttpStatusCode.NotFound)
    {
        _ = response.EnsureSuccessStatusCode();
    }

    var published = response.IsSuccessStatusCode;
    var publish = bool.Parse(args[2]);
    var output = Environment.GetEnvironmentVariable("GITHUB_OUTPUT") ?? throw new InvalidOperationException("GITHUB_OUTPUT is required.");
    await File.AppendAllLinesAsync(output, [$"generate={(!publish || !published).ToString().ToLowerInvariant()}"]).ConfigureAwait(false);
    if (publish && published)
    {
        Console.WriteLine($"Release {tag} already exists. Use a new pinned tag for changed files.");
    }

    return 0;
}

var files = Directory.GetFiles(args[2]);

if (files.Length == 0 || !File.Exists(Path.Combine(args[2], "voices.json")))
{
    throw new InvalidDataException("The voice folder must contain assets and voices.json.");
}

Array.Sort(files, StringComparer.Ordinal);

string[] arguments =
[
    "release", "create", tag, "--repo", repository,
    "--target", Environment.GetEnvironmentVariable("GITHUB_SHA") ?? throw new InvalidOperationException("GITHUB_SHA is required."),
    "--title", $"Voice files ({tag})",
    "--notes", "On-device Read Aloud voices. MeloTTS-English uses MIT. BERT, Kokoro and misaki use Apache-2.0. voices.json lists file sizes and SHA-256 hashes.",
    .. files,
];

return Process.Run("gh", arguments).ExitCode;
