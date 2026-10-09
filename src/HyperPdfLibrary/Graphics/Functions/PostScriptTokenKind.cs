// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>The kind of a PostScript calculator token.</summary>
internal enum PostScriptTokenKind
{
    /// <summary>The end of the program.</summary>
    End = 0,

    /// <summary>An opening brace.</summary>
    Open = 1,

    /// <summary>A closing brace.</summary>
    Close = 2,

    /// <summary>A number or an operator name.</summary>
    Word = 3,
}
