// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Http.GitHub;
using PdfViewerLite.Http.Remote;
using Refit;

namespace PdfViewerLite.Http;

/// <summary>
/// Creates Refit clients through the compile time generated implementations and source generated JSON metadata, so no
/// reflection is needed under Native AOT.
/// </summary>
public static class RefitClients
{
    /// <summary>Gets the GitHub API base address.</summary>
    public static Uri GitHubApiAddress { get; } = new("https://api.github.com");

    /// <summary>Creates a GitHub releases client.</summary>
    /// <param name="httpClient">The HTTP client; its base address defaults to the GitHub API.</param>
    /// <returns>The client.</returns>
    public static IGitHubReleasesApi CreateGitHubReleasesApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        httpClient.BaseAddress ??= GitHubApiAddress;
        return RestService.ForGenerated<IGitHubReleasesApi>(httpClient, HttpJsonContext.Default);
    }

    /// <summary>Creates a client that downloads documents from the HTTP client's base address.</summary>
    /// <param name="httpClient">The HTTP client with its base address set to the document host.</param>
    /// <returns>The client.</returns>
    public static IRemoteDocumentApi CreateRemoteDocumentApi(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        return RestService.ForGenerated<IRemoteDocumentApi>(httpClient, HttpJsonContext.Default);
    }
}
