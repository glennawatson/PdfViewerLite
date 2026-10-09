// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>Which accessibility checks apply to an annotation.</summary>
internal enum AnnotationKind
{
    /// <summary>A hidden annotation, a pop-up or a printer's mark: no checks.</summary>
    Skipped = 0,

    /// <summary>A link.</summary>
    Link = 1,

    /// <summary>A form field's widget.</summary>
    Widget = 2,

    /// <summary>Any other annotation.</summary>
    Other = 3,
}
