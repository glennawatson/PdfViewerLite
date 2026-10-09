// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>What the extractor inserts between two text runs.</summary>
internal enum TextInsertion
{
    /// <summary>The runs join with nothing between them.</summary>
    None = 0,

    /// <summary>A space.</summary>
    Space = 1,

    /// <summary>A line break.</summary>
    LineBreak = 2,

    /// <summary>Nothing, but the hyphen ending the previous line joins the word to this one.</summary>
    Hyphen = 3,
}
