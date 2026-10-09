// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>The Adobe character collection a CID font uses, from its /CIDSystemInfo ordering.</summary>
internal enum CjkScript
{
    /// <summary>Not a CJK collection.</summary>
    None = 0,

    /// <summary>The Adobe-Japan1 collection.</summary>
    Japanese = 1,

    /// <summary>Adobe-GB1, simplified Chinese.</summary>
    SimplifiedChinese = 2,

    /// <summary>Adobe-CNS1, traditional Chinese.</summary>
    TraditionalChinese = 3,

    /// <summary>The Adobe-Korea1 collection.</summary>
    Korean = 4,
}
