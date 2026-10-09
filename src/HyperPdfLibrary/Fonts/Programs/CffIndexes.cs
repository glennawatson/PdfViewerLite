// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The INDEXes of a CFF font that the program keeps.</summary>
/// <param name="CharStrings">The CharStrings INDEX.</param>
/// <param name="Strings">The String INDEX.</param>
/// <param name="GlobalSubrs">The Global Subr INDEX.</param>
[DebuggerDisplay("CffIndexes: {CharStrings.Count} charstrings")]
internal readonly record struct CffIndexes(CffIndex CharStrings, CffIndex Strings, CffIndex GlobalSubrs);
