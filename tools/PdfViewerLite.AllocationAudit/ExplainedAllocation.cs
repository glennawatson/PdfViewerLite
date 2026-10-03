// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.AllocationAudit;

/// <summary>An allocation that is expected, with the reason it is acceptable.</summary>
/// <param name="Type">The allocated type name, or a prefix of it.</param>
/// <param name="Frame">The PdfViewerLite method that allocates, or a prefix of it.</param>
/// <param name="Reason">Why the allocation is acceptable.</param>
[DebuggerDisplay("{Type} in {Frame}")]
public sealed record ExplainedAllocation(string Type, string Frame, string Reason);
