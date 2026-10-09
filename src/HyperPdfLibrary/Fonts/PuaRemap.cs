// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts;

/// <summary>The built-in encoding whose codes a font's private-use text stands for.</summary>
internal enum PuaRemap
{
    /// <summary>The font's text is left as it is.</summary>
    None = 0,

    /// <summary>The text follows the Symbol encoding.</summary>
    Symbol = 1,

    /// <summary>The text follows the ZapfDingbats encoding.</summary>
    Dingbats = 2,
}
