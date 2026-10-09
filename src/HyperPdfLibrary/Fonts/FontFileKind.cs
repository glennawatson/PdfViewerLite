// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>The font descriptor key an embedded font program came from.</summary>
internal enum FontFileKind
{
    /// <summary>The font is not embedded.</summary>
    None = 0,

    /// <summary>/FontFile: a Type 1 program.</summary>
    Type1 = 1,

    /// <summary>/FontFile2: a TrueType program.</summary>
    TrueType = 2,

    /// <summary>/FontFile3: a CFF or OpenType program named by the stream's /Subtype.</summary>
    Compact = 3,
}
