// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;
using PdfViewerLite.Core.Speech;

namespace PdfViewerLite.Tools.VoiceModels;

/// <summary>Reads the pinned ONNX source and checks the imported files.</summary>
internal static class MeloFiles
{
    /// <summary>The length of a SHA-256 digest written as hexadecimal.</summary>
    private const int DigestLength = 64;

    /// <summary>Reads the files to import.</summary>
    /// <param name="path">The source manifest.</param>
    /// <returns>The pinned files.</returns>
    /// <exception cref="InvalidDataException">The manifest has no files or has an invalid entry.</exception>
    internal static List<SpeechModelFile> Load(string path)
    {
        using var manifest = JsonDocument.Parse(File.ReadAllBytes(path));
        var source = new Uri(manifest.RootElement.GetProperty("source").GetString()!, UriKind.Absolute);
        var files = new List<SpeechModelFile>();
        HashSet<string> names = [with(comparer: StringComparer.OrdinalIgnoreCase)];
        foreach (var entry in manifest.RootElement.GetProperty(nameof(files)).EnumerateArray())
        {
            var file = ReadFile(entry, source);
            if (!names.Add(file.LocalName))
            {
                throw new InvalidDataException($"Duplicate source manifest entry: {file.LocalName}.");
            }

            files.Add(file);
        }

        if (files.Count == 0)
        {
            throw new InvalidDataException("The source manifest has no files.");
        }

        return files;
    }

    /// <summary>Checks every file against its pinned size and SHA-256.</summary>
    /// <param name="files">The pinned files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <returns>A task.</returns>
    /// <exception cref="InvalidDataException">A file differs from the pinned export.</exception>
    internal static async Task ValidateAsync(IReadOnlyList<SpeechModelFile> files, string directory)
    {
        foreach (var file in files)
        {
            var input = File.OpenRead(Path.Combine(directory, file.LocalName));
            await using var ownedInput = input.ConfigureAwait(false);
            if (input.Length != file.Bytes)
            {
                throw new InvalidDataException($"{file.LocalName} has the wrong size.");
            }

            var hash = await SHA256.HashDataAsync(input).ConfigureAwait(false);
            if (!string.Equals(Convert.ToHexStringLower(hash), file.Sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{file.LocalName} does not match its pinned SHA-256.");
            }

            Console.WriteLine($"verified {file.LocalName}");
        }
    }

    /// <summary>Reads and validates one manifest entry.</summary>
    /// <param name="entry">The entry.</param>
    /// <param name="source">The source release address.</param>
    /// <returns>The pinned file.</returns>
    /// <exception cref="InvalidDataException">The entry is invalid.</exception>
    private static SpeechModelFile ReadFile(JsonElement entry, Uri source)
    {
        var name = entry.GetProperty("name").GetString() ?? string.Empty;
        var bytes = entry.GetProperty("bytes").GetInt64();
        var sha256 = entry.GetProperty("sha256").GetString() ?? string.Empty;
        ValidateName(name);
        if (bytes <= 0 || sha256.Length != DigestLength)
        {
            throw new InvalidDataException($"Invalid source manifest entry: {name}.");
        }

        foreach (var digit in sha256)
        {
            if (!char.IsAsciiHexDigit(digit))
            {
                throw new InvalidDataException($"Invalid SHA-256 for {name}.");
            }
        }

        return new(new(source, name), name, bytes, sha256.ToLowerInvariant());
    }

    /// <summary>Checks that an asset name stays inside its output folder.</summary>
    /// <param name="name">The asset name.</param>
    /// <exception cref="InvalidDataException">The name is empty or contains a path.</exception>
    private static void ValidateName(string name)
    {
        if (string.IsNullOrWhiteSpace(name) || name is "." or ".." || name.AsSpan().IndexOfAny('/', '\\') >= 0 || Path.IsPathRooted(name))
        {
            throw new InvalidDataException($"Invalid source asset name: {name}.");
        }
    }
}
