// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts;

/// <summary>A family name to try when choosing a system font.</summary>
/// <param name="Family">The family name.</param>
/// <param name="Requested">Whether the PDF named it, rather than it being a generic stand-in.</param>
[DebuggerDisplay("FamilyCandidate: {Family}")]
internal readonly record struct FamilyCandidate(string Family, bool Requested);
