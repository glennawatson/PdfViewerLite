// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Refit;
namespace HyperPdfLibrary.Fonts.Generation;

/// <summary>Downloads individual pinned upstream font pack resources.</summary>
internal interface IFontDataApi
{
    /// <summary>Reads one binary resource.</summary>
    /// <param name="path">The repository path.</param>
    /// <param name="cancellationToken">Cancels the download.</param>
    /// <returns>The response owned by the caller.</returns>
    [Get("/{**path}")]
    Task<HttpResponseMessage> ReadAsync(string path, CancellationToken cancellationToken);
}
