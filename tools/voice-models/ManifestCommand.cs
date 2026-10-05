// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerLite.Tools.Commands;

/// <summary>Handles the manifest command.</summary>
internal static class ManifestCommand
{
    /// <summary>Runs the command.</summary>
    /// <param name="args">The command arguments.</param>
    /// <returns>The command exit code.</returns>
    internal static async Task<int> RunAsync(string[] args)
    {
        if (args.Length != 1)
        {
            await Console.Error.WriteLineAsync("Usage: PdfViewerLite.Tools voice manifest <release folder>");
            return 1;
        }

        var folder = Path.GetFullPath(args[0]);
        await using var output = new MemoryStream();
        var paths = Directory.GetFiles(folder);
        Array.Sort(paths, StringComparer.Ordinal);
        await using (var writer = new Utf8JsonWriter(output, new JsonWriterOptions { Indented = true }))
        {
            writer.WriteStartObject();
            writer.WriteStartArray("files");
            foreach (var path in paths)
            {
                var name = Path.GetFileName(path);
                if (name == "voices.json")
                {
                    continue;
                }

                await using var input = File.OpenRead(path);
                var digest = await SHA256.HashDataAsync(input).ConfigureAwait(false);
                writer.WriteStartObject();
                writer.WriteString(nameof(name), name);
                writer.WriteNumber("bytes", input.Length);
                writer.WriteString("sha256", Convert.ToHexStringLower(digest));
                writer.WriteEndObject();
            }

            writer.WriteEndArray();
            writer.WriteEndObject();
        }

        await File.WriteAllBytesAsync(Path.Combine(folder, "voices.json"), output.ToArray()).ConfigureAwait(false);
        await Console.Out.WriteLineAsync(System.Text.Encoding.UTF8.GetString(output.GetBuffer(), 0, checked((int)output.Length)));
        return 0;
    }
}
