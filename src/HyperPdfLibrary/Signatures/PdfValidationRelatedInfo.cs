// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Signatures;

/// <summary>A /VRI entry: the validation data saved for one signature.</summary>
/// <param name="Key">The key: the upper-case hex SHA-1 of the signature's /Contents.</param>
/// <param name="Certificates">The DER certificates.</param>
/// <param name="OcspResponses">The DER OCSP responses.</param>
/// <param name="Crls">The DER certificate revocation lists.</param>
/// <param name="CreatedAt">The /TU time the entry was made, if recorded.</param>
/// <param name="Timestamp">The /TS timestamp token over the signature, or <see langword="null"/>.</param>
[DebuggerDisplay("PdfValidationRelatedInfo: {Key}")]
public sealed record PdfValidationRelatedInfo(
    string Key,
    byte[][] Certificates,
    byte[][] OcspResponses,
    byte[][] Crls,
    DateTimeOffset? CreatedAt,
    byte[]? Timestamp);
