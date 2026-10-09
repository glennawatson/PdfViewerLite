// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The step an optimisation run is on, reported with its progress.</summary>
public enum PdfOptimizePhase
{
    /// <summary>Checking signatures, encryption and PDF/A claims.</summary>
    Checking = 0,

    /// <summary>Reading the pages to find how images and fonts are used.</summary>
    Analysing = 1,

    /// <summary>Changing pages: cleanup, accessibility entries, text layers and inferred tags.</summary>
    Editing = 2,

    /// <summary>Finding identical objects.</summary>
    Deduplicating = 3,

    /// <summary>Writing objects to the destination; images and fonts are re-encoded as they are written.</summary>
    Writing = 4,

    /// <summary>The run has finished.</summary>
    Done = 5,
}
