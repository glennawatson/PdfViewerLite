// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.Pdfium.Native;

/// <summary>PDFium <c>PDFACTION_*</c> values.</summary>
internal enum PdfActionType
{
    /// <summary>An unsupported action.</summary>
    Unsupported = 0,

    /// <summary>Go to a destination in this document.</summary>
    GoTo = 1,

    /// <summary>Go to a destination in another document.</summary>
    RemoteGoTo = 2,

    /// <summary>Open a URI.</summary>
    Uri = 3,

    /// <summary>Launch an application.</summary>
    Launch = 4,

    /// <summary>Go to a destination in an embedded document.</summary>
    EmbeddedGoTo = 5,
}
