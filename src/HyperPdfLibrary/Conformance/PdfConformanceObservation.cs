// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>One feature the reading report saw in the file.</summary>
/// <param name="Kind">What was seen.</param>
/// <param name="Count">How many times, or how many distinct fonts, files or streams. At least 1.</param>
/// <param name="Detail">A short description, such as the font names; null when there is nothing more to say.</param>
/// <param name="ConflictsWithClaim">Whether the file claims a PDF/A part that forbids the feature. Always false when the file claims no PDF/A part.</param>
[DebuggerDisplay("PdfConformanceObservation: {Kind} x{Count}")]
public readonly record struct PdfConformanceObservation(PdfConformanceFinding Kind, int Count, string? Detail, bool ConflictsWithClaim);
