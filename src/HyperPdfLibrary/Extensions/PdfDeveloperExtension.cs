// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Extensions;

/// <summary>A developer extension the document declares in the catalog's <c>/Extensions</c> dictionary.</summary>
/// <param name="Prefix">The developer prefix, for example ADBE.</param>
/// <param name="BaseVersion">The PDF version the extension builds on, for example "1.7".</param>
/// <param name="ExtensionLevel">The extension level.</param>
/// <param name="Url">A URL describing the extension, or null.</param>
[DebuggerDisplay("PdfDeveloperExtension: {Prefix} {BaseVersion} level {ExtensionLevel}")]
public sealed record PdfDeveloperExtension(string Prefix, string BaseVersion, int ExtensionLevel, string? Url);
