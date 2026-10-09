// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Where a reading node came from, so a reader can say how far to trust its order and roles.</summary>
public enum PdfNodeOrigin
{
    /// <summary>The document's own tags.</summary>
    Tagged = 0,

    /// <summary>Worked out from the page layout, because the page has no usable tags.</summary>
    Inferred = 1,

    /// <summary>Worked out from text recognition words, because the page is an image.</summary>
    Ocr = 2,
}
