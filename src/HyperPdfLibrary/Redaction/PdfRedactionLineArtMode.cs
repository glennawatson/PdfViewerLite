// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Redaction;

/// <summary>What redaction does to paths and shadings that a redacted area touches.</summary>
public enum PdfRedactionLineArtMode
{
    /// <summary>Leave line art alone.</summary>
    None = 0,

    /// <summary>Remove paths and shadings whose bounds lie wholly inside the area.</summary>
    RemoveCovered = 1,

    /// <summary>Remove every path and shading the area touches.</summary>
    RemoveTouched = 2,
}
