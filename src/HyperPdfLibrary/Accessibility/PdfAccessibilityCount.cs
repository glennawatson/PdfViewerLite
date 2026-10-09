// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>How many times the report found one kind of problem.</summary>
/// <param name="Code">The kind of finding.</param>
/// <param name="Count">The number of times it was found, including findings left out of the report's list by its cap.</param>
[DebuggerDisplay("PdfAccessibilityCount: {Code} x{Count}")]
public readonly record struct PdfAccessibilityCount(PdfAccessibilityCode Code, int Count);
