// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Document;

/// <summary>What a whole-document check found.</summary>
/// <param name="Faults">Each fault with its object number and offset: repairs made while opening and reading, then faults the check found.</param>
/// <param name="ObjectsChecked">The in-use objects read.</param>
/// <param name="StreamsChecked">The streams decoded.</param>
/// <param name="PagesChecked">The pages whose structure and content were checked.</param>
[DebuggerDisplay("PdfCheckReport: {Faults.Count} faults in {ObjectsChecked} objects")]
public sealed record PdfCheckReport(IReadOnlyList<PdfDiagnostic> Faults, int ObjectsChecked, int StreamsChecked, int PagesChecked)
{
    /// <summary>Gets a value indicating whether the check found nothing.</summary>
    public bool IsClean => Faults.Count == 0;
}
