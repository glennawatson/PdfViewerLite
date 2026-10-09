// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Interchange;

/// <summary>Stores streams as indirect objects, which a stream must be wherever it appears.</summary>
internal interface IInterchangeObjects
{
    /// <summary>Stores a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns>A reference to the stored stream.</returns>
    PdfValue Add(PdfStream stream);
}
