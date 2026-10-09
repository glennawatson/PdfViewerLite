// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>A shading painted over the current clip with <c>sh</c>.</summary>
[DebuggerDisplay("PdfShadingObject: {Name}")]
public sealed class PdfShadingObject : PdfPageObject
{
    /// <summary>Initializes a new instance of the <see cref="PdfShadingObject"/> class.</summary>
    internal PdfShadingObject()
    {
    }

    /// <inheritdoc/>
    public override PdfPageObjectKind Kind => PdfPageObjectKind.Shading;

    /// <summary>Gets the shading's name in the resources' /Shading.</summary>
    public PdfName Name { get; internal init; }

    /// <summary>Gets the shading dictionary, or <see langword="null"/> when the resource is missing.</summary>
    public PdfDictionary? Dictionary { get; internal init; }
}
