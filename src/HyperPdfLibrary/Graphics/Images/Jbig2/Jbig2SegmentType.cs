// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The JBIG2 segment types (T.88 section 7.3).</summary>
internal enum Jbig2SegmentType
{
    /// <summary>A symbol dictionary.</summary>
    SymbolDictionary = 0,

    /// <summary>A text region kept for refinement.</summary>
    IntermediateTextRegion = 4,

    /// <summary>A text region drawn on the page.</summary>
    ImmediateTextRegion = 6,

    /// <summary>A lossless text region drawn on the page.</summary>
    ImmediateLosslessTextRegion = 7,

    /// <summary>A pattern dictionary.</summary>
    PatternDictionary = 16,

    /// <summary>A halftone region kept for refinement.</summary>
    IntermediateHalftoneRegion = 20,

    /// <summary>A halftone region drawn on the page.</summary>
    ImmediateHalftoneRegion = 22,

    /// <summary>A lossless halftone region drawn on the page.</summary>
    ImmediateLosslessHalftoneRegion = 23,

    /// <summary>A generic region kept for refinement.</summary>
    IntermediateGenericRegion = 36,

    /// <summary>A generic region drawn on the page.</summary>
    ImmediateGenericRegion = 38,

    /// <summary>A lossless generic region drawn on the page.</summary>
    ImmediateLosslessGenericRegion = 39,

    /// <summary>A refinement region kept for further refinement.</summary>
    IntermediateRefinementRegion = 40,

    /// <summary>A refinement region drawn on the page.</summary>
    ImmediateRefinementRegion = 42,

    /// <summary>A lossless refinement region drawn on the page.</summary>
    ImmediateLosslessRefinementRegion = 43,

    /// <summary>The page information.</summary>
    PageInformation = 48,

    /// <summary>The end of the page.</summary>
    EndOfPage = 49,

    /// <summary>The end of a stripe.</summary>
    EndOfStripe = 50,

    /// <summary>The end of the file.</summary>
    EndOfFile = 51,

    /// <summary>The profiles the data conforms to.</summary>
    Profiles = 52,

    /// <summary>A custom Huffman table.</summary>
    Tables = 53,

    /// <summary>An extension.</summary>
    Extension = 62,
}
