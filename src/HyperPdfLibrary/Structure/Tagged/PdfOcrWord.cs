// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>A word found by text recognition on an image-only page, fed to <see cref="PdfReadingStructure.FromOcrWords"/>.</summary>
/// <param name="Text">The word.</param>
/// <param name="Bounds">Where it is, in viewer space.</param>
/// <param name="Confidence">How sure the recogniser is, from 0 to 100.</param>
[DebuggerDisplay("PdfOcrWord: {Text} ({Confidence})")]
public readonly record struct PdfOcrWord(string Text, PdfViewerRect Bounds, float Confidence);
