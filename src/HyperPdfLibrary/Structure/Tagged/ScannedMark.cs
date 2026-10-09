// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>One <c>BMC</c> or <c>BDC</c> found by <see cref="MarkedContentScanner"/>, in the order the interpreter runs it.</summary>
/// <param name="Tag">The tag.</param>
/// <param name="Mcid">The <c>/MCID</c> of its properties, or -1.</param>
/// <param name="Properties">The property list when it was read in full, otherwise <see langword="null"/>.</param>
[DebuggerDisplay("ScannedMark: {Tag} mcid {Mcid}")]
internal readonly record struct ScannedMark(PdfName Tag, int Mcid, PdfDictionary? Properties);
