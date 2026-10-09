// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PageObjects;

/// <summary>
/// A form XObject painted with <c>Do</c>. Its own content is read on demand with <see cref="GetContent"/>; changes made
/// there are written as a new form, so other pages that share the original form are not touched.
/// </summary>
[DebuggerDisplay("PdfFormObject: {Name}")]
public sealed class PdfFormObject : PdfPageObject
{
    /// <summary>The form's content, read on first use.</summary>
    private PdfPageContent? _content;

    /// <summary>Initializes a new instance of the <see cref="PdfFormObject"/> class.</summary>
    internal PdfFormObject()
    {
    }

    /// <inheritdoc/>
    public override PdfPageObjectKind Kind => PdfPageObjectKind.Form;

    /// <summary>Gets the form's name in the resources' /XObject.</summary>
    public PdfName Name { get; internal init; }

    /// <summary>Gets the form stream.</summary>
    public PdfStream Stream { get; internal init; } = null!;

    /// <summary>Gets the form's /Matrix, mapping form space to the space of the content that paints it.</summary>
    public Matrix3x2 FormMatrix { get; internal init; } = Matrix3x2.Identity;

    /// <summary>Gets the form's /BBox in form space, or <see langword="null"/> when it has none.</summary>
    public PdfRectangle? BoundingBox { get; internal init; }

    /// <inheritdoc/>
    public override bool IsModified => base.IsModified || _content?.IsModified == true;

    /// <summary>Gets the form's content if it has been read.</summary>
    internal PdfPageContent? ReadContent => _content;

    /// <summary>Reads the form's content into objects, once.</summary>
    /// <returns>The content; its objects carry matrices that include this form's matrix and the painting content's matrix.</returns>
    /// <exception cref="InvalidOperationException">The object is not part of a content that was read from a document.</exception>
    public PdfPageContent GetContent() =>
        _content ??= PdfPageContent.ReadForm(Owner ?? throw new InvalidOperationException("The form object has no owner."), this);
}
