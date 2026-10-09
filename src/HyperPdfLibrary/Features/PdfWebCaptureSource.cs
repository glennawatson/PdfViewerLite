// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>Where a web capture's content came from (a source information dictionary).</summary>
/// <param name="Url">The source URL, or null.</param>
/// <param name="Timestamp">When the content was retrieved, or null.</param>
/// <param name="Expires">When the content expires, or null.</param>
/// <param name="Type">The <c>/S</c> source type: 0 for a URL, 1 for a post request, 2 for a get request.</param>
[DebuggerDisplay("PdfWebCaptureSource: {Url}")]
public sealed record PdfWebCaptureSource(string? Url, DateTimeOffset? Timestamp, DateTimeOffset? Expires, int Type);
