// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Redaction;

/// <summary>What redaction does to annotations, links and form fields that a redacted area touches.</summary>
public enum PdfRedactionAnnotationMode
{
    /// <summary>Leave them alone.</summary>
    Keep = 0,

    /// <summary>Remove links, with their actions, and leave other annotations.</summary>
    RemoveLinks = 1,

    /// <summary>Remove every annotation the area touches, with its appearances and actions, and form fields whose widgets are removed.</summary>
    RemoveTouched = 2,
}
