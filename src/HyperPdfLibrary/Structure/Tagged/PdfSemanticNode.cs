// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// One node of a page's reading structure: a heading, paragraph, list, table, figure, link, form field and so on, with
/// its text, its box and the page it is on, so a reader can speak it and move the focus to it. Built once and then
/// never changed.
/// </summary>
[DebuggerDisplay("PdfSemanticNode: {Role} {Level} ({Origin}) {Text}")]
public sealed class PdfSemanticNode
{
    /// <summary>The child nodes, in reading order.</summary>
    private readonly List<PdfSemanticNode> _children = [];

    /// <summary>The header cells that label this table cell, made on first use.</summary>
    private List<PdfSemanticNode>? _headers;

    /// <summary>The indices of what the node holds directly.</summary>
    private int[] _items = [];

    /// <summary>Initializes a new instance of the <see cref="PdfSemanticNode"/> class.</summary>
    /// <param name="role">What the node is.</param>
    /// <param name="origin">Where the node came from.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    internal PdfSemanticNode(PdfSemanticRole role, PdfNodeOrigin origin, int pageIndex)
    {
        Role = role;
        Origin = origin;
        PageIndex = pageIndex;
    }

    /// <summary>Gets what the node is.</summary>
    public PdfSemanticRole Role { get; }

    /// <summary>Gets where the node came from: the tags, the layout or text recognition.</summary>
    public PdfNodeOrigin Origin { get; }

    /// <summary>Gets the zero based page index; the node's focus target is this page and <see cref="Bounds"/>.</summary>
    public int PageIndex { get; }

    /// <summary>Gets the parent node, or <see langword="null"/> for a top-level node.</summary>
    public PdfSemanticNode? Parent { get; private set; }

    /// <summary>Gets the child nodes, in reading order.</summary>
    public IReadOnlyList<PdfSemanticNode> Children => _children;

    /// <summary>Gets the structure element, for a tagged node.</summary>
    public PdfStructureElement? Element { get; internal set; }

    /// <summary>Gets the heading level, 1 to 6, for a heading; zero otherwise.</summary>
    public int Level { get; internal set; }

    /// <summary>Gets the marked content id, for a <see cref="PdfSemanticRole.Content"/> node; otherwise -1.</summary>
    public int Mcid { get; internal set; } = -1;

    /// <summary>Gets the node's box in viewer space; empty when it draws nothing on the page.</summary>
    public PdfViewerRect Bounds { get; internal set; }

    /// <summary>Gets the text the node draws, in reading order.</summary>
    public string Text { get; internal set; } = string.Empty;

    /// <summary>Gets the text that replaces the drawn text (<c>/ActualText</c>), or <see langword="null"/>.</summary>
    public string? ActualText { get; internal set; }

    /// <summary>
    /// Gets the indices of what the node holds directly: glyphs of <see cref="PdfMarkedContentPage"/> for tagged and
    /// inferred nodes, words for text recognition nodes.
    /// </summary>
    public ReadOnlySpan<int> Items => _items;

    /// <summary>Gets the annotation a link, form field or annotation node refers to, or <see langword="null"/>.</summary>
    public PdfDictionary? Annotation { get; internal set; }

    /// <summary>Gets the annotation's object id; not valid when there is none or it is written directly in the page.</summary>
    public PdfObjectId AnnotationId { get; internal set; }

    /// <summary>Gets a link's web address, or <see langword="null"/>.</summary>
    public string? LinkUri { get; internal set; }

    /// <summary>Gets a form field's widget and state, or <see langword="null"/>.</summary>
    public PdfFormWidget? FormField { get; internal set; }

    /// <summary>Gets a form field's label: its <c>/TU</c>, else its name; <see langword="null"/> for other nodes.</summary>
    public string? FieldLabel { get; internal set; }

    /// <summary>Gets a table cell's row, from zero; -1 for other nodes.</summary>
    public int Row { get; internal set; } = -1;

    /// <summary>Gets a table cell's column, from zero; -1 for other nodes.</summary>
    public int Column { get; internal set; } = -1;

    /// <summary>Gets a table cell's row span.</summary>
    public int RowSpan { get; internal set; } = 1;

    /// <summary>Gets a table cell's column span.</summary>
    public int ColumnSpan { get; internal set; } = 1;

    /// <summary>Gets which cells a header cell labels: from its <c>/Scope</c>, or inferred from its place.</summary>
    public PdfTableScope Scope { get; internal set; }

    /// <summary>Gets the header cells that label this table cell: its <c>/Headers</c>, or inferred from the header scopes.</summary>
    public IReadOnlyList<PdfSemanticNode> Headers => _headers ?? (IReadOnlyList<PdfSemanticNode>)[];

    /// <summary>Gets the alternate description (<c>/Alt</c>), or <see langword="null"/>.</summary>
    public string? AlternateText => Element?.AlternateText;

    /// <summary>Gets the language, or <see langword="null"/>.</summary>
    public string? Language => Element?.Language;

    /// <summary>
    /// Gets the text a reader should speak: the replacement text, else a figure's or formula's description, else the
    /// drawn text, else the description, else a form field's label.
    /// </summary>
    public string SpokenText => ActualText ?? DescribedText() ?? DrawnText();

    /// <summary>Gets a value indicating whether the node is read by its description rather than its drawn text.</summary>
    private bool IsDescribed => Role is PdfSemanticRole.Figure or PdfSemanticRole.Formula;

    /// <summary>Appends the items of the node and its descendants, in reading order.</summary>
    /// <param name="output">Receives the item indices.</param>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public void CollectItems(List<int> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        output.AddRange(_items);
        foreach (var child in _children)
        {
            child.CollectItems(output);
        }
    }

    /// <summary>Adds a child node.</summary>
    /// <param name="child">The child.</param>
    internal void Add(PdfSemanticNode child)
    {
        child.Parent = this;
        _children.Add(child);
    }

    /// <summary>Sets the indices of what the node holds directly.</summary>
    /// <param name="items">The indices.</param>
    internal void SetItems(int[] items) => _items = items;

    /// <summary>Adds a header cell that labels this cell.</summary>
    /// <param name="header">The header cell.</param>
    internal void AddHeader(PdfSemanticNode header)
    {
        _headers ??= [];
        if (!_headers.Contains(header))
        {
            _headers.Add(header);
        }
    }

    /// <summary>Gets a figure's or formula's description, which it is read by.</summary>
    /// <returns>The description, or <see langword="null"/>.</returns>
    private string? DescribedText() => IsDescribed ? AlternateText : null;

    /// <summary>Gets the drawn text, else the description, else a form field's label.</summary>
    /// <returns>The text; empty when there is none.</returns>
    private string DrawnText() => Text.Length > 0 ? Text : AlternateText ?? FieldLabel ?? string.Empty;
}
