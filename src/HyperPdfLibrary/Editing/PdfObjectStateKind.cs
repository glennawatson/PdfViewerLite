// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>Where an object's current value comes from in the edit layer.</summary>
internal enum PdfObjectStateKind
{
    /// <summary>Not edited: the value is the one in the file, or nothing for a number the file never used.</summary>
    Original = 0,

    /// <summary>Changed or added: the edit layer holds the value.</summary>
    Edited = 1,

    /// <summary>Deleted since opening.</summary>
    Deleted = 2,
}
