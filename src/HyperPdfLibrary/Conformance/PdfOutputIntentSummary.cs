// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>An output intent as the conformance report lists it.</summary>
/// <param name="Subtype">The intent type, for example GTS_PDFA1 or GTS_PDFX.</param>
/// <param name="OutputConditionIdentifier">The identifier of the output condition, or null.</param>
/// <param name="RegistryName">The registry's URL, or null.</param>
/// <param name="Info">Extra text about the condition, or null.</param>
/// <param name="HasProfile">Whether the intent has a <c>/DestOutputProfile</c> stream.</param>
/// <param name="ProfileComponents">The <c>/N</c> of the profile stream, or 0 without a profile.</param>
/// <param name="ProfileColorSpace">The device colour space the component count implies: GRAY, RGB or CMYK; null for other counts or no profile.</param>
[DebuggerDisplay("PdfOutputIntentSummary: {Subtype} {OutputConditionIdentifier}")]
public sealed record PdfOutputIntentSummary(
    string Subtype,
    string? OutputConditionIdentifier,
    string? RegistryName,
    string? Info,
    bool HasProfile,
    int ProfileComponents,
    string? ProfileColorSpace);
