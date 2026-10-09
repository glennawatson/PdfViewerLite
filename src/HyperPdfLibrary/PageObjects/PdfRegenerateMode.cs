// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.PageObjects;

/// <summary>How <see cref="PdfPageContent"/> writes the objects it did not change.</summary>
public enum PdfRegenerateMode
{
    /// <summary>Unchanged objects keep the bytes they were read from.</summary>
    Preserve = 0,

    /// <summary>Every object is written again from the model, as a check that the model holds everything the content says.</summary>
    Rewrite = 1,
}
