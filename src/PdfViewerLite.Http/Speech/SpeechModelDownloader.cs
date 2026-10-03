// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Speech;

namespace PdfViewerLite.Http.Speech;

/// <summary>
/// Downloads an on-device voice's files with Refit. Each file is written to its own <c>.part</c> file and moved into
/// place once complete, so an interrupted download is never mistaken for a finished one and two downloads at once do
/// not collide; files already present are kept.
/// </summary>
public static class SpeechModelDownloader
{
    /// <summary>The copy buffer size.</summary>
    private const int BufferSize = 81_920;

    /// <summary>Gets the files not yet downloaded.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <returns>The missing files.</returns>
    public static List<SpeechModelFile> Missing(IReadOnlyList<SpeechModelFile> files, string directory)
    {
        ArgumentNullException.ThrowIfNull(files);
        ArgumentException.ThrowIfNullOrEmpty(directory);
        var missing = new List<SpeechModelFile>(files.Count);
        foreach (var file in files)
        {
            if (!File.Exists(Path.Combine(directory, file.LocalName)))
            {
                missing.Add(file);
            }
        }

        return missing;
    }

    /// <summary>Gets about how many bytes a set of files needs.</summary>
    /// <param name="files">The files.</param>
    /// <returns>The approximate total.</returns>
    public static long ApproximateBytes(IReadOnlyList<SpeechModelFile> files)
    {
        ArgumentNullException.ThrowIfNull(files);
        var total = 0L;
        foreach (var file in files)
        {
            total += file.ApproximateBytes;
        }

        return total;
    }

    /// <summary>Downloads the missing files.</summary>
    /// <param name="files">The files.</param>
    /// <param name="directory">The voice folder.</param>
    /// <param name="progress">Receives the fraction done, 0 to 1.</param>
    /// <param name="cancellationToken">Cancels the download, leaving finished files in place.</param>
    /// <returns>A task.</returns>
    /// <exception cref="HttpRequestException">Thrown when a server fails a request.</exception>
    /// <exception cref="InvalidDataException">Thrown when a file's name would place it outside the voice folder.</exception>
    public static async Task DownloadAsync(IReadOnlyList<SpeechModelFile> files, string directory, IProgress<double> progress, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(progress);
        var missing = Missing(files, directory);
        var total = Math.Max(1L, ApproximateBytes(missing));
        var done = 0L;
        var buffer = new byte[BufferSize];
        foreach (var file in missing)
        {
            var target = Path.GetFullPath(Path.Combine(directory, file.LocalName));
            if (!target.StartsWith(Path.GetFullPath(directory), StringComparison.Ordinal))
            {
                throw new InvalidDataException($"{file.LocalName} is outside the voice folder.");
            }

            _ = Directory.CreateDirectory(Path.GetDirectoryName(target)!);
            var partial = $"{target}.{Guid.NewGuid():N}.part";
            using (var response = await RefitClients.Download(file.Source, cancellationToken).ConfigureAwait(false))
            {
                _ = response.EnsureSuccessStatusCode();
                var source = await response.Content.ReadAsStreamAsync(cancellationToken).ConfigureAwait(false);
                await using (source.ConfigureAwait(false))
                {
                    var output = File.Create(partial);
                    await using (output.ConfigureAwait(false))
                    {
                        int read;
                        while ((read = await source.ReadAsync(buffer, cancellationToken).ConfigureAwait(false)) > 0)
                        {
                            await output.WriteAsync(buffer.AsMemory(0, read), cancellationToken).ConfigureAwait(false);
                            done += read;
                            progress.Report(Math.Min(1D, (double)done / total));
                        }
                    }
                }
            }

            File.Move(partial, target, true);
        }

        progress.Report(1D);
    }
}
