// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>What parsing a shading needs.</summary>
/// <param name="Value">The shading dictionary or stream.</param>
/// <param name="Spaces">The /ColorSpace resources.</param>
[DebuggerDisplay("ShadingRequest")]
internal readonly record struct ShadingRequest(PdfValue Value, PdfDictionary? Spaces);
