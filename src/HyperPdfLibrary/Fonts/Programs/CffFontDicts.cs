// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>What a CID-keyed CFF font's Font DICTs give the program.</summary>
/// <param name="Privates">The Private DICT of each Font DICT.</param>
/// <param name="Matrix">The first Font DICT's matrix, when it has one.</param>
[DebuggerDisplay("CffFontDicts: {Privates.Length} private dictionaries")]
internal readonly record struct CffFontDicts(CffPrivate[] Privates, FontMatrix? Matrix);
