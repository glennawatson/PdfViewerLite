// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Walks the structure tree once, in logical order, and reports what the element types alone show: types that mean
/// nothing, heading order, figures, tables and lists. It also notes which element owns each annotation, so the page
/// checks can tell whether an annotation is tagged.
/// </summary>
[DebuggerDisplay("AccessibilityStructureScan: {_elementCount} elements")]
internal sealed class AccessibilityStructureScan
{
    /// <summary>The most role map steps followed when looking for a loop.</summary>
    private const int MaxRoleMapSteps = 32;

    /// <summary>Set in a table's flags when it has a header cell.</summary>
    private const int HasHeaderCell = 1;

    /// <summary>Set in a table's flags when a header cell states its scope.</summary>
    private const int HasScope = 2;

    /// <summary>Set in a table's flags when a cell lists its headers.</summary>
    private const int HasHeadersList = 4;

    /// <summary>The findings.</summary>
    private readonly AccessibilityFindings _findings;

    /// <summary>The structure tree.</summary>
    private readonly PdfStructureTree _tree;

    /// <summary>The role map, or <see langword="null"/>.</summary>
    private readonly PdfDictionary? _roleMap;

    /// <summary>The claimed PDF/UA part, or <see langword="null"/>.</summary>
    private readonly int? _part;

    /// <summary>The cancellation token.</summary>
    private readonly CancellationToken _cancellation;

    /// <summary>Whether each raw type's role map loops, by spelling.</summary>
    private readonly Dictionary<string, bool> _cycles = [with(StringComparer.Ordinal)];

    /// <summary>The elements that refer to each object, by object number.</summary>
    private readonly Dictionary<int, PdfStructureElement> _owners = [];

    /// <summary>The pages tables start on; -1 stands for a table whose page is unknown.</summary>
    private readonly HashSet<int> _tablePages = [];

    /// <summary>The number of elements visited.</summary>
    private int _elementCount;

    /// <summary>The level of the last heading, or zero before the first.</summary>
    private int _lastHeading;

    /// <summary>The number of level 1 headings seen.</summary>
    private int _levelOneCount;

    /// <summary>Initializes a new instance of the <see cref="AccessibilityStructureScan"/> class.</summary>
    /// <param name="tree">The structure tree.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token.</param>
    internal AccessibilityStructureScan(PdfStructureTree tree, int? part, AccessibilityFindings findings, CancellationToken cancellation)
    {
        _tree = tree;
        _part = part;
        _findings = findings;
        _cancellation = cancellation;
        _roleMap = tree.Root.GetDictionary(KnownName.RoleMap);
    }

    /// <summary>Gets a value indicating whether the tree uses the PDF 2.0 structure namespace.</summary>
    internal bool UsesPdf20Namespace { get; private set; }

    /// <summary>Determines whether a page may hold part of a table, so its reading nodes are worth building.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <returns><see langword="true"/> when a table starts on the page, or starts on a page the file does not name.</returns>
    internal bool MayHoldTable(int pageIndex) => _tablePages.Contains(pageIndex) || _tablePages.Contains(-1);

    /// <summary>Finds the element that refers to an annotation or other object.</summary>
    /// <param name="id">The object's id.</param>
    /// <returns>The element, or <see langword="null"/>.</returns>
    internal PdfStructureElement? FindOwner(PdfObjectId id) => id.IsValid && _owners.TryGetValue(id.Number, out var owner) ? owner : null;

    /// <summary>Walks the whole tree.</summary>
    internal void Run()
    {
        foreach (var root in _tree.Roots)
        {
            Visit(root);
        }
    }

    /// <summary>Reads what a table's cells state, without entering tables nested inside it.</summary>
    /// <param name="element">The table or one of its descendants.</param>
    /// <param name="isRoot">Whether the element is the table itself.</param>
    /// <returns>The flags: header cell, scope and headers list.</returns>
    private static int CollectTableFlags(PdfStructureElement element, bool isRoot)
    {
        if (!isRoot && element.Type == PdfStructureType.Table)
        {
            return 0;
        }

        var flags = 0;
        if (element.Type == PdfStructureType.TableHeaderCell)
        {
            flags |= HasHeaderCell;
            flags |= element.Attributes.Scope != PdfTableScope.None ? HasScope : 0;
        }

        flags |= element.Attributes.Headers.Length > 0 ? HasHeadersList : 0;
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                flags |= CollectTableFlags(child, false);
            }
        }

        return flags;
    }

    /// <summary>Visits an element, then its kids.</summary>
    /// <param name="element">The element.</param>
    private void Visit(PdfStructureElement element)
    {
        _cancellation.ThrowIfCancellationRequested();
        _elementCount++;
        UsesPdf20Namespace |= element.Namespace == PdfStructureTypes.Pdf20Uri;
        CheckType(element);
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Element && kid.Element is { } child)
            {
                Visit(child);
            }
            else if (kid.Kind == PdfStructureKidKind.Object && kid.Object.IsValid)
            {
                _ = _owners.TryAdd(kid.Object.Number, element);
            }
        }
    }

    /// <summary>Runs the check that belongs to an element's type.</summary>
    /// <param name="element">The element.</param>
    private void CheckType(PdfStructureElement element)
    {
        switch (element.Type)
        {
            case PdfStructureType.Unknown:
            {
                CheckUnknown(element);
                break;
            }

            case PdfStructureType.Figure:
            {
                CheckFigure(element);
                break;
            }

            case PdfStructureType.Table:
            {
                CheckTable(element);
                break;
            }

            case PdfStructureType.ListItem:
            {
                CheckListItem(element);
                break;
            }

            case >= PdfStructureType.Heading1 and <= PdfStructureType.Heading6:
            {
                CheckHeading(element);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Reports a type that resolves to no standard type, as a loop when the role map goes round in a circle.</summary>
    /// <param name="element">The element.</param>
    private void CheckUnknown(PdfStructureElement element)
    {
        var code = element.Namespace is null && HasRoleMapCycle(element.RawType)
            ? PdfAccessibilityCode.RoleMapCycle
            : PdfAccessibilityCode.UnmappedStructureType;
        _findings.Add(code, element.PageIndex, element.Id);
    }

    /// <summary>Determines whether following the role map from a type comes back to a type already seen.</summary>
    /// <param name="type">The type's spelling.</param>
    /// <returns><see langword="true"/> when the map loops.</returns>
    private bool HasRoleMapCycle(string type)
    {
        if (_roleMap is null)
        {
            return false;
        }

        if (_cycles.TryGetValue(type, out var known))
        {
            return known;
        }

        var seen = new HashSet<string>(StringComparer.Ordinal);
        var current = type;
        var loops = false;
        for (var step = 0; step < MaxRoleMapSteps && current is not null && !loops; step++)
        {
            loops = !seen.Add(current);
            current = _roleMap.NameText(current);
        }

        _cycles[type] = loops;
        return loops;
    }

    /// <summary>Reports a figure with no description.</summary>
    /// <param name="element">The figure.</param>
    private void CheckFigure(PdfStructureElement element)
    {
        if (element.AlternateText is null && element.ActualText is null)
        {
            _findings.Add(PdfAccessibilityCode.FigureNoDescription, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a list item outside a list.</summary>
    /// <param name="element">The list item.</param>
    private void CheckListItem(PdfStructureElement element)
    {
        if (element.Parent is not { Type: PdfStructureType.List })
        {
            _findings.Add(PdfAccessibilityCode.ListItemOutsideList, element.PageIndex, element.Id);
        }
    }

    /// <summary>Reports a heading that skips a level, and a second level 1 heading.</summary>
    /// <param name="element">The heading.</param>
    private void CheckHeading(PdfStructureElement element)
    {
        var level = element.HeadingLevel;
        if (level > _lastHeading + 1)
        {
            _findings.Add(PdfAccessibilityCode.HeadingLevelSkipped, element.PageIndex, element.Id);
        }

        if (level == 1)
        {
            _levelOneCount++;
            if (_levelOneCount > 1 && _part != PdfUaClaim.Part2)
            {
                _findings.Add(PdfAccessibilityCode.MultipleLevelOneHeadings, element.PageIndex, element.Id);
            }
        }

        _lastHeading = level;
    }

    /// <summary>Reports a table with no header cells, or with header cells that say nothing about what they head.</summary>
    /// <param name="table">The table.</param>
    private void CheckTable(PdfStructureElement table)
    {
        _ = _tablePages.Add(table.PageIndex);
        var flags = CollectTableFlags(table, true);
        if ((flags & HasHeaderCell) == 0)
        {
            _findings.Add(PdfAccessibilityCode.TableNoHeaderCells, table.PageIndex, table.Id);
        }
        else if ((flags & (HasScope | HasHeadersList)) == 0)
        {
            _findings.Add(PdfAccessibilityCode.TableHeaderNoScope, table.PageIndex, table.Id);
        }
    }
}
