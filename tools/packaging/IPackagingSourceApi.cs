// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace PdfViewerLite.Tools.Packaging;

/// <summary>Downloads pinned packaging sources.</summary>
[Headers("User-Agent: PdfViewerLite-Packaging")]
internal interface IPackagingSourceApi
{
    /// <summary>Downloads a source archive.</summary>
    /// <param name="address">The pinned address.</param>
    /// <param name="cancellationToken">Cancels the request.</param>
    /// <returns>The response owned by the caller.</returns>
    [Get("")]
    Task<HttpResponseMessage> DownloadAsync([Url] Uri address, CancellationToken cancellationToken);
}
