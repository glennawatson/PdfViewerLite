// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;

namespace PdfViewerLite.Tools.VoiceModels;

/// <summary>Downloads pinned voice assets.</summary>
[Headers("User-Agent: PdfViewerLite-VoiceModels")]
internal interface IVoiceAssetApi
{
    /// <summary>Downloads an asset.</summary>
    /// <param name="address">The absolute source address.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The response owned by the caller.</returns>
    [Get("")]
    Task<HttpResponseMessage> DownloadAsync([Url] Uri address, CancellationToken cancellationToken);
}
