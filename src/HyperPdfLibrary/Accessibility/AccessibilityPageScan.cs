// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Checks one page at a time: its tab order, the text it draws outside any tag, its tables as the reader sees them,
/// and each annotation's tag, description and label.
/// </summary>
[DebuggerDisplay("AccessibilityPageScan: {_document.PageCount} pages")]
internal sealed class AccessibilityPageScan
{
    /// <summary>The annotation flag bit for Hidden.</summary>
    private const int HiddenFlag = 1 << 1;

    /// <summary>The annotation flag bit for NoView.</summary>
    private const int NoViewFlag = 1 << 5;

    /// <summary>The most <c>/Parent</c> steps followed to find a field's label.</summary>
    private const int MaxFieldDepth = 32;

    /// <summary>The <c>/Tabs</c> value that makes the tab order follow the structure.</summary>
    private const string StructureTabs = "S";

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The structure tree, or <see langword="null"/> for an untagged document.</summary>
    private readonly PdfStructureTree? _tree;

    /// <summary>The structure walk, or <see langword="null"/> for an untagged document.</summary>
    private readonly AccessibilityStructureScan? _structure;

    /// <summary>The claimed PDF/UA part, or <see langword="null"/>.</summary>
    private readonly int? _part;

    /// <summary>The findings.</summary>
    private readonly AccessibilityFindings _findings;

    /// <summary>The tables already checked, so a table that spans pages is judged once.</summary>
    private readonly HashSet<PdfStructureElement> _checkedTables = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>Initializes a new instance of the <see cref="AccessibilityPageScan"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="structure">The structure walk, or <see langword="null"/> for an untagged document.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    internal AccessibilityPageScan(PdfDocument document, AccessibilityStructureScan? structure, int? part, AccessibilityFindings findings)
    {
        _document = document;
        _tree = PdfDocumentTagged.GetStructureTree(document);
        _structure = structure;
        _part = part;
        _findings = findings;
    }

    /// <summary>Checks a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>What was counted on the page.</returns>
    internal PdfAccessibilityPage Check(int pageIndex)
    {
        var page = PdfDocumentPages.GetPage(_document, pageIndex);
        var content = PdfDocumentTagged.GetMarkedContent(_document, pageIndex);
        CountGlyphs(content, out var untagged, out var artifacts);
        if (_tree is not null && untagged > 0)
        {
            _findings.Add(PdfAccessibilityCode.UntaggedContent, pageIndex);
        }

        var reachable = CheckAnnotations(page, pageIndex);
        var tabs = page.Dictionary.NameText("Tabs") == StructureTabs;
        if (reachable > 0 && !tabs)
        {
            _findings.Add(PdfAccessibilityCode.TabsNotStructure, pageIndex);
        }

        if (_structure?.MayHoldTable(pageIndex) == true)
        {
            CheckTables(pageIndex);
        }

        return new(pageIndex, reachable, tabs, untagged, artifacts);
    }

    /// <summary>Counts the visible glyphs drawn outside marked content and outside artifacts, and those drawn as artifacts.</summary>
    /// <param name="content">What the page draws.</param>
    /// <param name="untagged">Receives the glyphs drawn outside marked content and artifacts.</param>
    /// <param name="artifacts">Receives the glyphs drawn as artifacts.</param>
    private static void CountGlyphs(PdfMarkedContentPage content, out int untagged, out int artifacts)
    {
        untagged = 0;
        artifacts = 0;
        var glyphs = content.Glyphs;
        for (var i = 0; i < glyphs.Length; i++)
        {
            ref readonly var glyph = ref glyphs[i];
            if (glyph.IsArtifact)
            {
                artifacts += IsVisible(content.GetText(i)) ? 1 : 0;
            }
            else if (glyph.Mcid < 0)
            {
                untagged += IsVisible(content.GetText(i)) ? 1 : 0;
            }
        }
    }

    /// <summary>Determines whether a glyph's text shows: a glyph with no text counts, white space does not.</summary>
    /// <param name="text">The glyph's text.</param>
    /// <returns><see langword="true"/> when it shows.</returns>
    private static bool IsVisible(ReadOnlySpan<char> text)
    {
        if (text.IsEmpty)
        {
            return true;
        }

        foreach (var character in text)
        {
            if (!char.IsWhiteSpace(character))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether an annotation is one a reader can reach, and which checks apply to it.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The kind.</returns>
    private static AnnotationKind Classify(PdfDictionary annotation)
    {
        var hidden = (annotation.GetInt32(KnownName.F, 0) & (HiddenFlag | NoViewFlag)) != 0;
        return hidden ? AnnotationKind.Skipped : annotation.NameText("Subtype") switch
        {
            "Popup" or "PrinterMark" or "TrapNet" => AnnotationKind.Skipped,
            "Link" => AnnotationKind.Link,
            "Widget" => AnnotationKind.Widget,
            _ => AnnotationKind.Other,
        };
    }

    /// <summary>Reads a field's label, its <c>/TU</c>, inherited from its parents.</summary>
    /// <param name="widget">The widget.</param>
    /// <returns><see langword="true"/> when the field has a label.</returns>
    private static bool HasFieldLabel(PdfDictionary widget)
    {
        var node = widget;
        for (var depth = 0; node is not null && depth < MaxFieldDepth; depth++)
        {
            if (node.GetText(KnownName.TU) is { Length: > 0 })
            {
                return true;
            }

            node = node.GetDictionary(KnownName.Parent);
        }

        return false;
    }

    /// <summary>Counts a table's cells that no header cell labels, without entering nested tables.</summary>
    /// <param name="node">The table or one of its descendants.</param>
    /// <param name="isRoot">Whether the node is the table itself.</param>
    /// <param name="hasHeader">Set when the table has a header cell.</param>
    /// <returns>The number of unlabelled data cells.</returns>
    private static int CountUnlabelledCells(PdfSemanticNode node, bool isRoot, ref bool hasHeader)
    {
        if (!isRoot && node.Role == PdfSemanticRole.Table)
        {
            return 0;
        }

        hasHeader |= node.Role == PdfSemanticRole.TableHeaderCell;
        var count = node.Role == PdfSemanticRole.TableCell && node.Headers.Count == 0 ? 1 : 0;
        foreach (var child in node.Children)
        {
            count += CountUnlabelledCells(child, false, ref hasHeader);
        }

        return count;
    }

    /// <summary>Checks every annotation on a page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns>The number of annotations a reader can reach.</returns>
    private int CheckAnnotations(PdfPage page, int pageIndex)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        var reachable = 0;
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            var kind = Classify(annotation);
            if (kind == AnnotationKind.Skipped)
            {
                continue;
            }

            reachable++;
            CheckAnnotation(annotation, annotations.GetRaw(i).AsReference(), kind, pageIndex);
        }

        return reachable;
    }

    /// <summary>Checks one annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">Its object id.</param>
    /// <param name="kind">Which checks apply.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckAnnotation(PdfDictionary annotation, PdfObjectId id, AnnotationKind kind, int pageIndex)
    {
        var owner = FindOwner(annotation, id);
        switch (kind)
        {
            case AnnotationKind.Link:
                {
                    CheckLink(annotation, owner, pageIndex);
                    break;
                }

            case AnnotationKind.Widget:
                {
                    CheckWidget(annotation, owner, pageIndex);
                    break;
                }

            default:
                {
                    CheckOther(owner, pageIndex);
                    break;
                }
        }
    }

    /// <summary>Finds the structure element that refers to an annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="id">Its object id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    private PdfStructureElement? FindOwner(PdfDictionary annotation, PdfObjectId id) =>
        _structure?.FindOwner(id) ?? _tree?.GetObjectParent(annotation);

    /// <summary>Checks that a link is tagged as a link and has a description.</summary>
    /// <param name="link">The link annotation.</param>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckLink(PdfDictionary link, PdfStructureElement? owner, int pageIndex)
    {
        var id = owner?.Id ?? default;
        if (_tree is not null && owner?.Type != PdfStructureType.Link)
        {
            _findings.Add(PdfAccessibilityCode.LinkNotTagged, pageIndex, id);
        }

        if (link.GetText(KnownName.Contents) is not { Length: > 0 } && owner?.AlternateText is null)
        {
            _findings.Add(PdfAccessibilityCode.LinkNoDescription, pageIndex, id);
        }
    }

    /// <summary>Checks that a form field has a label and is tagged as a form field.</summary>
    /// <param name="widget">The widget annotation.</param>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckWidget(PdfDictionary widget, PdfStructureElement? owner, int pageIndex)
    {
        var id = owner?.Id ?? default;
        if (!HasFieldLabel(widget))
        {
            _findings.Add(PdfAccessibilityCode.FormFieldNoLabel, pageIndex, id);
        }

        if (_tree is not null && owner?.Type != PdfStructureType.Form)
        {
            _findings.Add(PdfAccessibilityCode.FormFieldNotTagged, pageIndex, id);
        }
    }

    /// <summary>Checks that any other annotation is referred to from the tree, as an Annot for PDF/UA-1.</summary>
    /// <param name="owner">The element that refers to it, or <see langword="null"/>.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckOther(PdfStructureElement? owner, int pageIndex)
    {
        if (_tree is null)
        {
            return;
        }

        if (owner is null)
        {
            _findings.Add(PdfAccessibilityCode.AnnotationNotTagged, pageIndex);
        }
        else if (_part != PdfUaClaim.Part2 && owner.Type != PdfStructureType.Annotation)
        {
            _findings.Add(PdfAccessibilityCode.AnnotationWrongStructureType, pageIndex, owner.Id);
        }
    }

    /// <summary>Checks the tables on a page as the reader sees them, once the header cells are linked to the cells they label.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckTables(int pageIndex)
    {
        foreach (var node in PdfReadingStructure.ReadTagged(_document, pageIndex).Nodes)
        {
            CheckTableNodes(node, pageIndex);
        }
    }

    /// <summary>Walks a node and its descendants for tables.</summary>
    /// <param name="node">The node.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    private void CheckTableNodes(PdfSemanticNode node, int pageIndex)
    {
        if (node.Role == PdfSemanticRole.Table && node.Element is { } element && _checkedTables.Add(element))
        {
            var hasHeader = false;
            if (CountUnlabelledCells(node, true, ref hasHeader) > 0 && hasHeader)
            {
                _findings.Add(PdfAccessibilityCode.TableCellNoHeader, pageIndex, element.Id);
            }
        }

        foreach (var child in node.Children)
        {
            CheckTableNodes(child, pageIndex);
        }
    }
}
