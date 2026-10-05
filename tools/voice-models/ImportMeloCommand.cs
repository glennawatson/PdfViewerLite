// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Tools.VoiceModels;
using Refit;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the import melo command.</summary>
internal static class ImportMeloCommand
{
    /// <summary>The shared download client.</summary>
    private static readonly HttpClient Client = new() { BaseAddress = new("https://github.com") };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        const int minArguments = 2;
        const int maxArguments = 3;
        if (args.Length is < minArguments or > maxArguments)
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools voice import-melo <source manifest> <output folder> [<local source folder>]");
            return 1;
        }

        var files = MeloFiles.Load(args[0]);
        var output = Path.GetFullPath(args[1]);
        var staging = Path.Combine(output, $".melo-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(staging);
        try
        {
            var api = RestService.ForGenerated<IVoiceAssetApi>(Client);
            foreach (var file in files)
            {
                var target = Path.Combine(staging, file.LocalName);
                if (args.Length == maxArguments)
                {
                    File.Copy(Path.Combine(args[2], file.LocalName), target);
                }
                else
                {
                    using var response = await api.DownloadAsync(file.Source, CancellationToken.None).ConfigureAwait(false);
                    _ = response.EnsureSuccessStatusCode();
                    await using var source = await response.Content.ReadAsStreamAsync().ConfigureAwait(false);
                    await using var destination = File.Create(target);
                    await source.CopyToAsync(destination).ConfigureAwait(false);
                }
            }

            await MeloFiles.ValidateAsync(files, staging).ConfigureAwait(false);
            foreach (var file in files)
            {
                File.Move(Path.Combine(staging, file.LocalName), Path.Combine(output, file.LocalName), true);
                await Console.Out.WriteLineAsync($"imported {file.LocalName}");
            }
        }
        finally
        {
            Directory.Delete(staging, true);
        }

        return 0;
    }
}
