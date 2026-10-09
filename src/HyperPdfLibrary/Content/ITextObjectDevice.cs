// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Content;

/// <summary>
/// A device that only collects text. The interpreter tells it where each text-showing operator starts, so glyphs group
/// into the same text objects PDFium makes; it has no use for images.
/// </summary>
internal interface ITextObjectDevice : IContentDevice
{
    /// <summary>Marks the start of a text-showing operator (<c>Tj</c>, <c>TJ</c>, <c>'</c> or <c>"</c>).</summary>
    void BeginTextObject();
}
