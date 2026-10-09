// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;

namespace HyperPdfLibrary.Annotations;

/// <summary>The far ends of the two sides of an open arrow head; both sides start at the arrow's point.</summary>
/// <param name="First">The end of the first side.</param>
/// <param name="Second">The end of the second side.</param>
[DebuggerDisplay("PdfArrowHead: {First} {Second}")]
public readonly record struct PdfArrowHead(Vector2 First, Vector2 Second);
