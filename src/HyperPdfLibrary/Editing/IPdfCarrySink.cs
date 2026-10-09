// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// What <see cref="PdfDocumentCarrier"/> needs from a page copier: copying values from the source, reserving and filling
/// target objects, and reading and replacing the target's catalog. An open document implements it through a transaction;
/// a new document through its builder.
/// </summary>
internal interface IPdfCarrySink
{
    /// <summary>Gets the objects copied from.</summary>
    PdfObjectStore Source { get; }

    /// <summary>Gets the target's objects, or <see langword="null"/> for a new document that has none yet.</summary>
    PdfObjectStore? TargetStore { get; }

    /// <summary>Gets the target's name table.</summary>
    PdfNameTable TargetNames { get; }

    /// <summary>Gets the target's catalog entries before any carrying; empty for a new document.</summary>
    PdfDictionary TargetCatalog { get; }

    /// <summary>Copies a source value and everything it refers to.</summary>
    /// <param name="value">The source value.</param>
    /// <returns>The value in the target.</returns>
    PdfValue Import(PdfValue value);

    /// <summary>Gets a source name in the target's name table.</summary>
    /// <param name="name">The source name.</param>
    /// <returns>The name.</returns>
    PdfName ImportName(PdfName name);

    /// <summary>Gets the target number of a source object.</summary>
    /// <param name="sourceNumber">The source object number.</param>
    /// <returns>The target number; 0 when not copied and -1 when left out.</returns>
    int GetMapped(int sourceNumber);

    /// <summary>Makes references to a source object point at an object already in the target.</summary>
    /// <param name="source">The source object.</param>
    /// <param name="target">The target object.</param>
    void MapObject(PdfObjectId source, PdfObjectId target);

    /// <summary>Makes the copy of a source object come from a replacement dictionary instead.</summary>
    /// <param name="sourceNumber">The source object number.</param>
    /// <param name="replacement">The dictionary copied in its place.</param>
    /// <param name="keepParent">Whether the replacement's <c>/Parent</c> entry is kept, naming an object already mapped.</param>
    void Override(int sourceNumber, PdfDictionary replacement, bool keepParent);

    /// <summary>Reserves an object number in the target.</summary>
    /// <returns>The new object's id.</returns>
    PdfObjectId Reserve();

    /// <summary>Sets the value of a target object.</summary>
    /// <param name="id">The object id.</param>
    /// <param name="value">The value.</param>
    void Set(PdfObjectId id, PdfValue value);

    /// <summary>Replaces the target's catalog entries.</summary>
    /// <param name="catalog">The catalog with the carried entries.</param>
    void SetCatalog(PdfDictionary catalog);
}
