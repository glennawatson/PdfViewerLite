// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace PdfViewerLite.AllocationAudit;

/// <summary>Allocations of one type made by one PdfViewerLite method.</summary>
/// <param name="Type">The allocated type.</param>
/// <param name="Frame">The innermost PdfViewerLite method on the stack.</param>
/// <param name="Count">The number of sampled allocations.</param>
/// <param name="Bytes">The sampled bytes.</param>
/// <param name="Explanation">The explanation, or <see langword="null"/> when unexplained.</param>
[DebuggerDisplay("{Type} x{Count} in {Frame}")]
public sealed record AllocationSite(string Type, string Frame, long Count, long Bytes, ExplainedAllocation? Explanation);
