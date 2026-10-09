// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Changes objects as they are written, so a re-encoded image or font is held in memory only while it is written.</summary>
internal interface IObjectTransformer
{
    /// <summary>Gets the value to write for an object.</summary>
    /// <param name="oldNumber">The object's number in the source.</param>
    /// <param name="value">The object's value.</param>
    /// <returns>The value to write: the same value, or a replacement that references only objects the original referenced.</returns>
    PdfValue Transform(int oldNumber, PdfValue value);

    /// <summary>Reports how far writing has got.</summary>
    /// <param name="written">The objects written.</param>
    /// <param name="total">The objects to write.</param>
    /// <param name="bytes">The bytes written to the destination.</param>
    void Progress(int written, int total, long bytes);
}
