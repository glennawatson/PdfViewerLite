// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Objects;

/// <summary>An object changed, added or deleted since a document was opened, as seen at one moment.</summary>
/// <param name="Number">The object number.</param>
/// <param name="Value">The new value; null for a deleted object.</param>
/// <param name="Deleted">Whether the object was deleted.</param>
/// <param name="Generation">The generation to write: the one the object was read with, or the one a deleted object's free entry takes.</param>
[DebuggerDisplay("Object {Number} gen {Generation} deleted {Deleted}")]
public readonly record struct PdfEditedObject(int Number, PdfValue Value, bool Deleted, int Generation);
