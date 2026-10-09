// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Redaction;

/// <summary>How overlay text sits across a redacted area (the annotation's <c>/Q</c>).</summary>
public enum PdfRedactionAlignment
{
    /// <summary>Flush with the left edge.</summary>
    Left = 0,

    /// <summary>In the middle of the area.</summary>
    Centre = 1,

    /// <summary>Flush with the right edge.</summary>
    Right = 2,
}
