// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Fonts;

/// <summary>A referenced stream awaiting inspection for font use.</summary>
/// <param name="Stream">The content stream.</param>
/// <param name="Resources">The visible resources.</param>
internal readonly record struct FontDataContent(PdfStream Stream, FontResourceScope Resources);
