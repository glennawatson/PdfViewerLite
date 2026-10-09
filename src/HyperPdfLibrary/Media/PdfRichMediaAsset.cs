// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>An asset in a rich media annotation's <c>/Assets</c> name tree.</summary>
/// <param name="Name">The asset name (the name tree key, usually a file name).</param>
/// <param name="Data">The embedded file stream, or null.</param>
[DebuggerDisplay("PdfRichMediaAsset: {Name}")]
public sealed record PdfRichMediaAsset(string Name, PdfStream? Data);
