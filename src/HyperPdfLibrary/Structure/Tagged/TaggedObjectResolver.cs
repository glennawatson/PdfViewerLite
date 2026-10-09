// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Attaches the objects that <c>/OBJR</c> kids refer to: a link annotation and its address, or a form widget with its
/// label and its state from the form reader.
/// </summary>
[DebuggerDisplay("TaggedObjectResolver: page {_page.Index}")]
internal sealed class TaggedObjectResolver
{
    /// <summary>The most <c>/Parent</c> steps followed to find a field's label.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The page.</summary>
    private readonly PdfPage _page;

    /// <summary>The page's form widgets, read on first use.</summary>
    private List<PdfFormWidget>? _widgets;

    /// <summary>Initializes a new instance of the <see cref="TaggedObjectResolver"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    internal TaggedObjectResolver(PdfDocument document, PdfPage page)
    {
        _document = document;
        _page = page;
    }

    /// <summary>Attaches an object reference to its element's node when the object is on the page.</summary>
    /// <param name="node">The element's node.</param>
    /// <param name="kid">The <c>/OBJR</c> kid.</param>
    internal void Attach(PdfSemanticNode node, in PdfStructureKid kid)
    {
        if (node.Annotation is not null || _document.Objects.GetObject(kid.Object).AsDictionary() is not { } target)
        {
            return;
        }

        // An annotation counts only while the page lists it; another object, such as an XObject, by the kid's page.
        var index = FindAnnotationIndex(kid.Object);
        var onPage = target.ContainsKey(KnownName.Rect) ? index >= 0 : kid.PageIndex == _page.Index;
        if (!onPage)
        {
            return;
        }

        node.Annotation = target;
        node.AnnotationId = kid.Object;
        if (target.TryGetRectangle(KnownName.Rect, out var rectangle))
        {
            node.Bounds = node.Bounds.Union(PdfViewerRect.FromViewerRectangle(_page.ToViewerRectangle(rectangle)));
        }

        Describe(node, target, index);
    }

    /// <summary>Reads a field's label: its <c>/TU</c>, inherited from its parents.</summary>
    /// <param name="widget">The widget dictionary.</param>
    /// <returns>The label, or <see langword="null"/>.</returns>
    private static string? ReadToolTip(PdfDictionary widget)
    {
        var node = widget;
        for (var depth = 0; node is not null && depth < MaxFieldDepth; depth++)
        {
            if (node.GetText(KnownName.TU) is { Length: > 0 } label)
            {
                return label;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return null;
    }

    /// <summary>Reads a link annotation's web address.</summary>
    /// <param name="link">The link annotation.</param>
    /// <returns>The address, or <see langword="null"/>.</returns>
    private static string? ReadUri(PdfDictionary link) =>
        link.GetDictionary(KnownName.A) is { } action && action.IsName(KnownName.S, KnownName.URI) ? action.GetText(KnownName.URI) : null;

    /// <summary>Reads a widget's partial field name, for a widget the form reader does not list.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns>The name, or <see langword="null"/>.</returns>
    private static string? ReadPartialName(PdfDictionary widget) =>
        widget.GetText(KnownName.T) ?? widget.GetDictionary(KnownName.Parent)?.GetText(KnownName.T);

    /// <summary>Fills in what a link or form field node needs.</summary>
    /// <param name="node">The node.</param>
    /// <param name="target">The annotation.</param>
    /// <param name="index">Its index in the page's <c>/Annots</c>, or -1.</param>
    private void Describe(PdfSemanticNode node, PdfDictionary target, int index)
    {
        switch (target.GetName(KnownName.Subtype).ToKnownName())
        {
            case KnownName.Link:
            {
                node.LinkUri = ReadUri(target);
                break;
            }

            case KnownName.Widget:
            {
                node.FormField = FindWidget(index);
                node.FieldLabel = ReadToolTip(target) ?? node.FormField?.Name ?? ReadPartialName(target);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Finds an annotation's index in the page's <c>/Annots</c>.</summary>
    /// <param name="id">The annotation's object id.</param>
    /// <returns>The index, or -1.</returns>
    private int FindAnnotationIndex(PdfObjectId id)
    {
        var annotations = _page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; id.IsValid && annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetRaw(i).AsReference().Number == id.Number)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Finds the form reader's widget at an annotation index.</summary>
    /// <param name="index">The index in the page's <c>/Annots</c>.</param>
    /// <returns>The widget, or <see langword="null"/>.</returns>
    private PdfFormWidget? FindWidget(int index)
    {
        if (index < 0)
        {
            return null;
        }

        if (_widgets is null)
        {
            _widgets = [];
            _document.Form.GetWidgets(_page.Index, _widgets);
        }

        foreach (var widget in _widgets)
        {
            if (widget.Index == index)
            {
                return widget;
            }
        }

        return null;
    }
}
