// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Media;

/// <summary>An instance in a rich media configuration.</summary>
/// <param name="Subtype">The instance type: 3D, Flash, Sound or Video.</param>
/// <param name="AssetName">The name of the asset it plays, or null.</param>
/// <param name="Parameters">The <c>/Params</c> dictionary, or null.</param>
[DebuggerDisplay("PdfRichMediaInstance: {Subtype} {AssetName}")]
public sealed record PdfRichMediaInstance(string Subtype, string? AssetName, PdfDictionary? Parameters);
