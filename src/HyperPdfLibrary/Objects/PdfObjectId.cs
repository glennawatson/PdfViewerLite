// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Objects;

/// <summary>Identifies an indirect object.</summary>
/// <param name="Number">The object number.</param>
/// <param name="Generation">The generation number.</param>
[DebuggerDisplay("{Number} {Generation} R")]
public readonly record struct PdfObjectId(int Number, int Generation)
{
    /// <summary>Gets a value indicating whether this identifies an object; direct objects have number zero.</summary>
    public bool IsValid => Number > 0;
}
