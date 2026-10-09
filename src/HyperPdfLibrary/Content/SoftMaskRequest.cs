// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>What recording a soft mask needs.</summary>
/// <param name="Owner">The interpreter that sets the mask.</param>
/// <param name="Mask">The soft mask dictionary.</param>
[DebuggerDisplay("SoftMaskRequest")]
internal readonly record struct SoftMaskRequest(ContentInterpreter Owner, PdfDictionary Mask);
