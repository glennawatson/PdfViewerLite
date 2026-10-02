// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using System.Text.Json;

namespace PdfViewerLite.Compatibility.Tests;

/// <summary>
/// Finds the corpus documents in the cache, downloading and checking them when
/// <c>PDFVIEWERLITE_CORPUS_DOWNLOAD=1</c>; otherwise tests for missing documents are skipped.
/// </summary>
internal static class RealWorldPdfCache
{
    /// <summary>The variable naming another cache folder.</summary>
    private const string DirectoryVariable = "PDFVIEWERLITE_CORPUS_DIR";

    /// <summary>The variable that allows downloading.</summary>
    private const string DownloadVariable = "PDFVIEWERLITE_CORPUS_DOWNLOAD";

    /// <summary>The longest a single download may take.</summary>
    private static readonly TimeSpan DownloadTimeout = TimeSpan.FromMinutes(5);

    /// <summary>The client for every download.</summary>
    private static readonly HttpClient Client = new() { Timeout = DownloadTimeout };

    /// <summary>Downloads one file at a time, so parallel tests share each download.</summary>
    private static readonly SemaphoreSlim Gate = new(1, 1);

    /// <summary>Gets the cache folder.</summary>
    internal static string Directory { get; } = Environment.GetEnvironmentVariable(DirectoryVariable) is { Length: > 0 } configured
        ? configured
        : Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");

    /// <summary>Gets the documents listed in the manifest.</summary>
    internal static IReadOnlyList<RealWorldPdf> Documents { get; } = Load();

    /// <summary>Gets a document's local path, downloading it when allowed.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The path.</returns>
    internal static Task<string> GetAsync(RealWorldPdf document) =>
        GetAsync(document.Url, Path.Combine(Directory, $"{document.Id}.pdf"), document.Sha256);

    /// <summary>Gets a document's reading order transcript.</summary>
    /// <param name="document">The document, which must have one.</param>
    /// <returns>The Markdown text.</returns>
    internal static async Task<string> GetGroundTruthAsync(RealWorldPdf document)
    {
        var path = await GetAsync(document.GroundTruthUrl!, Path.Combine(Directory, $"{document.Id}.md"), null);
        return await File.ReadAllTextAsync(path);
    }

    /// <summary>Gets a file, downloading it when allowed.</summary>
    /// <param name="url">Where it comes from.</param>
    /// <param name="path">Where it is cached.</param>
    /// <param name="sha256">Its expected hash, or <see langword="null"/> to accept any content.</param>
    /// <returns>The path.</returns>
    /// <exception cref="TUnit.Core.Exceptions.SkipTestException">The file is missing and downloading is not allowed.</exception>
    /// <exception cref="InvalidDataException">The download does not match its hash.</exception>
    private static async Task<string> GetAsync(Uri url, string path, string? sha256)
    {
        if (File.Exists(path))
        {
            return path;
        }

        if (Environment.GetEnvironmentVariable(DownloadVariable) != "1")
        {
            throw new TUnit.Core.Exceptions.SkipTestException($"{Path.GetFileName(path)} is not in {Directory}; set {DownloadVariable}=1 to download the corpus.");
        }

        await Gate.WaitAsync();
        try
        {
            if (File.Exists(path))
            {
                return path;
            }

            _ = System.IO.Directory.CreateDirectory(Directory);
            var bytes = await Client.GetByteArrayAsync(url);
            if (sha256 is not null && !string.Equals(Convert.ToHexStringLower(SHA256.HashData(bytes)), sha256, StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{url} does not match its SHA-256 in the manifest.");
            }

            var part = $"{path}.{Guid.NewGuid():N}.part";
            await File.WriteAllBytesAsync(part, bytes);
            File.Move(part, path, true);
            return path;
        }
        finally
        {
            _ = Gate.Release();
        }
    }

    /// <summary>Reads the manifest copied next to the tests.</summary>
    /// <returns>The documents.</returns>
    private static IReadOnlyList<RealWorldPdf> Load()
    {
        using var stream = File.OpenRead(Path.Combine(AppContext.BaseDirectory, "corpus.json"));
        return JsonSerializer.Deserialize(stream, RealWorldPdfJsonContext.Default.RealWorldPdfManifest)!.Documents;
    }
}
