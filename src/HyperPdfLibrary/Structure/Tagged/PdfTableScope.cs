// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Which cells a table header cell labels, from its <c>/Scope</c> attribute or inferred from its place.</summary>
public enum PdfTableScope
{
    /// <summary>Not given.</summary>
    None = 0,

    /// <summary>The cells to its right in its rows.</summary>
    Row = 1,

    /// <summary>The cells below it in its columns.</summary>
    Column = 2,

    /// <summary>Both its rows and its columns.</summary>
    Both = 3,
}
