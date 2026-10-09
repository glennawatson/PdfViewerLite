// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;

namespace HyperPdfLibrary.Document;

/// <content>The effect pending edits would have on signatures.</content>
public sealed partial class PdfDocument
{
    /// <summary>
    /// Reports, for each signature field, whether saving the pending edits keeps the signature valid and whether its
    /// DocMDP and FieldMDP permissions allow the kinds of change made. This only reports; it blocks nothing.
    /// </summary>
    /// <param name="incremental">Whether the save appends an update; a full rewrite always breaks signatures.</param>
    /// <returns>The report.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfSignatureReport GetSignatureEffects(bool incremental) => PdfSignatureEffects.Analyze(this, incremental);
}
