// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Builds one page's reading nodes from the structure tree: each element with content or an object on the page
/// becomes a node, and each marked content id a content leaf holding its glyphs. A marked content id is read once,
/// by the first element in logical order that claims it, as PDFium reads shared content.
/// </summary>
[DebuggerDisplay("TaggedNodeBuilder: page {_pageIndex}")]
internal sealed class TaggedNodeBuilder
{
    /// <summary>The page.</summary>
    private readonly PdfPage _page;

    /// <summary>The zero based page index.</summary>
    private readonly int _pageIndex;

    /// <summary>What the page draws.</summary>
    private readonly PdfMarkedContentPage _content;

    /// <summary>The marked content ids already given to a node.</summary>
    private readonly HashSet<int> _used = [];

    /// <summary>Builds node text.</summary>
    private readonly StringBuilder _text = new();

    /// <summary>Resolves object references to annotations and form fields.</summary>
    private readonly TaggedObjectResolver _objects;

    /// <summary>Initializes a new instance of the <see cref="TaggedNodeBuilder"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    internal TaggedNodeBuilder(PdfDocument document, int pageIndex)
    {
        _pageIndex = pageIndex;
        _page = document.GetPage(pageIndex);
        _content = document.GetMarkedContent(pageIndex);
        _objects = new(document, _page);
    }

    /// <summary>Gets what the page draws.</summary>
    internal PdfMarkedContentPage Content => _content;

    /// <summary>Builds the page's top-level nodes.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="output">Receives the nodes, in logical order.</param>
    internal void Build(PdfStructureTree tree, List<PdfSemanticNode> output)
    {
        foreach (var root in tree.Roots)
        {
            if (BuildElement(root) is { } node)
            {
                output.Add(node);
            }
        }
    }

    /// <summary>Joins two pieces of text, adding a space between words that would otherwise run together.</summary>
    /// <param name="text">The text so far.</param>
    /// <param name="next">The next piece.</param>
    private static void AppendWord(StringBuilder text, string next)
    {
        if (next.Length == 0)
        {
            return;
        }

        if (text.Length > 0 && !char.IsWhiteSpace(text[^1]) && !char.IsWhiteSpace(next[0]))
        {
            _ = text.Append(' ');
        }

        _ = text.Append(next);
    }

    /// <summary>Builds an element's node, or nothing when it has nothing on the page.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The node, or <see langword="null"/>.</returns>
    private PdfSemanticNode? BuildElement(PdfStructureElement element)
    {
        if (element.Type == PdfStructureType.Artifact)
        {
            return null;
        }

        var node = new PdfSemanticNode(element.Role, PdfNodeOrigin.Tagged, _pageIndex) { Element = element, Level = element.HeadingLevel, ActualText = element.ActualText };

        foreach (var kid in element.Kids)
        {
            AddKid(node, kid);
        }

        if (!Belongs(node, element))
        {
            return null;
        }

        Finish(node);
        return node;
    }

    /// <summary>Determines whether a node shows on the page: it holds content or an object there, or describes something there.</summary>
    /// <param name="node">The node.</param>
    /// <param name="element">Its element.</param>
    /// <returns><see langword="true"/> when the page should read it.</returns>
    private bool Belongs(PdfSemanticNode node, PdfStructureElement element) =>
        node.Children.Count > 0 || node.Annotation is not null
        || (element.PageIndex == _pageIndex && (element.ActualText is not null || element.AlternateText is not null));

    /// <summary>Adds one kid to an element's node.</summary>
    /// <param name="node">The element's node.</param>
    /// <param name="kid">The kid.</param>
    private void AddKid(PdfSemanticNode node, in PdfStructureKid kid)
    {
        switch (kid.Kind)
        {
            case PdfStructureKidKind.Element when kid.Element is { } child:
            {
                if (BuildElement(child) is { } childNode)
                {
                    node.Add(childNode);
                }

                break;
            }

            case PdfStructureKidKind.MarkedContent when IsOnPage(kid.PageIndex):
            {
                if (BuildContent(kid.Mcid) is { } leaf)
                {
                    node.Add(leaf);
                }

                break;
            }

            case PdfStructureKidKind.Object:
            {
                _objects.Attach(node, kid);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Determines whether a kid's page is this page; a kid whose page is unknown counts, as PDFium reads it.</summary>
    /// <param name="pageIndex">The kid's page.</param>
    /// <returns><see langword="true"/> when it is on this page.</returns>
    private bool IsOnPage(int pageIndex) => pageIndex == _pageIndex || pageIndex < 0;

    /// <summary>Makes the content leaf of a marked content id, once.</summary>
    /// <param name="mcid">The marked content id.</param>
    /// <returns>The leaf, or <see langword="null"/> when the id was read before or draws nothing.</returns>
    private PdfSemanticNode? BuildContent(int mcid)
    {
        if (mcid < 0 || !_used.Add(mcid) || !_content.HasContent(mcid))
        {
            return null;
        }

        var glyphs = _content.GetGlyphs(mcid);
        _ = _text.Clear();
        foreach (var glyph in glyphs)
        {
            _ = _text.Append(_content.GetText(glyph));
        }

        var leaf = new PdfSemanticNode(PdfSemanticRole.Content, PdfNodeOrigin.Tagged, _pageIndex) { Mcid = mcid, Text = _text.ToString(), Bounds = _content.GetBounds(mcid) };
        leaf.SetItems(glyphs.ToArray());
        return leaf;
    }

    /// <summary>Works out an element node's text and box from its children, and links a table's cells to their headers.</summary>
    /// <param name="node">The node.</param>
    private void Finish(PdfSemanticNode node)
    {
        _ = _text.Clear();
        var bounds = node.Bounds;
        foreach (var child in node.Children)
        {
            AppendWord(_text, child.Text);
            bounds = bounds.Union(child.Bounds);
        }

        node.Text = _text.ToString();
        node.Bounds = bounds.IsEmpty ? LayoutBounds(node.Element) : bounds;
        if (node.Role == PdfSemanticRole.Table)
        {
            TableHeaderResolver.Resolve(node);
        }
    }

    /// <summary>Gets an element's Layout <c>/BBox</c> in viewer space, for an element that draws nothing else.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The box, or empty.</returns>
    private PdfViewerRect LayoutBounds(PdfStructureElement? element) =>
        element?.Attributes.BoundingBox is { } box && element.PageIndex == _pageIndex
            ? PdfViewerRect.FromViewerRectangle(_page.ToViewerRectangle(box))
            : default;
}
