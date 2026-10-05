// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using PdfViewerLite.Core.Speech;
using PdfViewerLite.Speech;
using PdfViewerLite.Tools.VoiceModels;
using Refit;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the mirror kokoro command.</summary>
internal static class MirrorKokoroCommand
{
    /// <summary>The shared download client.</summary>
    private static readonly HttpClient Client = new() { BaseAddress = new("https://github.com") };

    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        const string kokoro = "https://huggingface.co/onnx-community/Kokoro-82M-v1.0-ONNX/resolve/1939ad2a8e416c0acfeecc08a694d14ef25f2231/";
        const string misaki = "https://raw.githubusercontent.com/hexgrad/misaki/fba1236595f2d2bf21d414ba6e57d25256afada3/misaki/data/";
        const int downloadMinutes = 20;
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools voice mirror-kokoro <output folder>");
            return 1;
        }

        var output = Path.GetFullPath(args[0]);
        var staging = Path.Combine(output, $".kokoro-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(staging);
        (string Name, string Address)[] assets = [
            ("kokoro-model_uint8.onnx", $"{kokoro}onnx/model_uint8.onnx"),
            ("kokoro-af_heart.bin", $"{kokoro}voices/af_heart.bin"),
            ("kokoro-af_bella.bin", $"{kokoro}voices/af_bella.bin"),
            ("kokoro-am_michael.bin", $"{kokoro}voices/am_michael.bin"),
            ("kokoro-bf_emma.bin", $"{kokoro}voices/bf_emma.bin"),
            ("kokoro-bm_george.bin", $"{kokoro}voices/bm_george.bin"),
            ("misaki-us_gold.json", $"{misaki}us_gold.json"),
            ("misaki-us_silver.json", $"{misaki}us_silver.json"),
            ("misaki-gb_gold.json", $"{misaki}gb_gold.json"),
            ("misaki-gb_silver.json", $"{misaki}gb_silver.json"),
        ];
        try
        {
            var api = RestService.ForGenerated<IVoiceAssetApi>(Client);
            using var cancellation = new CancellationTokenSource(TimeSpan.FromMinutes(downloadMinutes));
            foreach (var (name, address) in assets)
            {
                using var response = await api.DownloadAsync(new(address), cancellation.Token).ConfigureAwait(false);
                _ = response.EnsureSuccessStatusCode();
                await using var source = await response.Content.ReadAsStreamAsync(cancellation.Token).ConfigureAwait(false);
                await using var destination = File.Create(Path.Combine(staging, name));
                await source.CopyToAsync(destination, cancellation.Token).ConfigureAwait(false);
            }

            var files = new List<SpeechModelFile>(assets.Length);
            foreach (var (name, _) in assets)
            {
                files.Add(VoiceRelease.File(name, name));
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
