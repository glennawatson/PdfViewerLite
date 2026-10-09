// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Features;

/// <summary>An output intent: the colour characteristics the document was prepared for.</summary>
/// <param name="Subtype">The intent type, for example GTS_PDFX, GTS_PDFA1 or ISO_PDFE1.</param>
/// <param name="OutputCondition">A human readable description, or null.</param>
/// <param name="OutputConditionIdentifier">The identifier of the condition, for example a registry name.</param>
/// <param name="RegistryName">The registry's URL, or null.</param>
/// <param name="Info">Extra text, or null.</param>
/// <param name="ComponentCount">The number of colour components in the profile (<c>/N</c> of the stream), or 0.</param>
/// <param name="Profile">The ICC profile stream (<c>/DestOutputProfile</c>), or null.</param>
[DebuggerDisplay("PdfOutputIntent: {Subtype} {OutputConditionIdentifier}")]
public sealed record PdfOutputIntent(
    string Subtype,
    string? OutputCondition,
    string? OutputConditionIdentifier,
    string? RegistryName,
    string? Info,
    int ComponentCount,
    PdfStream? Profile);
