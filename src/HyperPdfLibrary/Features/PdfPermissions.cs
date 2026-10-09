// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Features;

/// <summary>The permissions dictionary (<c>/Perms</c>) and the legal attestation (<c>/Legal</c>).</summary>
/// <param name="HasDocMdp">Whether a certification signature (<c>/DocMDP</c>) is present.</param>
/// <param name="DocMdpLevel">The change level the certifier allows (1 none, 2 form filling and signing, 3 also annotations), or null when the signature names none.</param>
/// <param name="HasUsageRights">Whether a usage rights signature (<c>/UR3</c>) is present.</param>
/// <param name="Legal">The counts in the legal attestation by key, for example <c>JavaScriptActions</c>.</param>
/// <param name="Attestation">The attestation text, or null.</param>
[DebuggerDisplay("PdfPermissions: DocMDP {DocMdpLevel}")]
public sealed record PdfPermissions(bool HasDocMdp, int? DocMdpLevel, bool HasUsageRights, IReadOnlyDictionary<string, long> Legal, string? Attestation);
