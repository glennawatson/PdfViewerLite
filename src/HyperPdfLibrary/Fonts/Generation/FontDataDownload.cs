// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;
namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Downloads only the binary resource requested by a document.</summary>
internal static class FontDataDownload
{
    /// <summary>The maximum time for a resource request.</summary>
    private const int TimeoutSeconds = 30;

    /// <summary>The shared generated download client.</summary>
    private static readonly IFontDataApi Api = RestService.ForGenerated<IFontDataApi>(
        new HttpClient { BaseAddress = new("https://raw.githubusercontent.com"), Timeout = TimeSpan.FromSeconds(TimeoutSeconds) },
        FontDataJsonContext.Default);

    /// <summary>Reads one pinned binary resource from a font pack.</summary>
    /// <param name="path">The repository path.</param>
    /// <param name="cancellationToken">Cancels I/O.</param>
    /// <returns>The resource bytes.</returns>
    internal static async ValueTask<byte[]> ReadAsync(string path, CancellationToken cancellationToken)
    {
        using var response = await Api.ReadAsync(path, cancellationToken).ConfigureAwait(false);
        _ = response.EnsureSuccessStatusCode();
        return await response.Content.ReadAsByteArrayAsync(cancellationToken).ConfigureAwait(false);
    }
}
