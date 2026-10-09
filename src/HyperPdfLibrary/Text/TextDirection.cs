// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>The writing direction of a character or run of characters, as PDFium's bidi segmenter groups them.</summary>
internal enum TextDirection
{
    /// <summary>Spaces, punctuation and symbols, which take the direction around them.</summary>
    Neutral = 0,

    /// <summary>Left-to-right letters.</summary>
    Left = 1,

    /// <summary>Right-to-left letters, such as Hebrew and Arabic.</summary>
    Right = 2,

    /// <summary>Numbers, separators and marks, which keep left-to-right order without changing the line direction.</summary>
    LeftWeak = 3,
}
