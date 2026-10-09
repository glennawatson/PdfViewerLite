// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Accessibility;

/// <summary>What an accessibility report finding is about. The values are stable, so callers may store them.</summary>
public enum PdfAccessibilityCode
{
    /// <summary>No finding. Never reported.</summary>
    None = 0,

    /// <summary>The catalog's <c>/MarkInfo /Marked</c> flag is missing or false.</summary>
    NotMarked = 1,

    /// <summary>The file has no structure tree, so screen readers get no tags.</summary>
    NoStructureTree = 2,

    /// <summary>The catalog has no <c>/Lang</c> entry.</summary>
    NoLanguage = 3,

    /// <summary>The XMP metadata has no <c>dc:title</c>.</summary>
    NoTitle = 4,

    /// <summary><c>DisplayDocTitle</c> in the viewer preferences is not true.</summary>
    DisplayDocTitleOff = 5,

    /// <summary>The <c>/MarkInfo /Suspects</c> flag is true, so the producer doubts the tags.</summary>
    SuspectsSet = 6,

    /// <summary>The file claims PDF/UA-2 but its version is below PDF 2.0.</summary>
    ClaimPart2NotPdf2 = 7,

    /// <summary>The file has a <c>pdfuaid:part</c> value other than 1 or 2.</summary>
    ClaimPartUnknown = 8,

    /// <summary>A page has annotations but its <c>/Tabs</c> entry is not <c>S</c>.</summary>
    TabsNotStructure = 9,

    /// <summary>A page draws text that is neither tagged nor marked as an artifact.</summary>
    UntaggedContent = 10,

    /// <summary>A structure type is not standard and no role map leads it to a standard type.</summary>
    UnmappedStructureType = 11,

    /// <summary>The role map sends a structure type round in a circle.</summary>
    RoleMapCycle = 12,

    /// <summary>A heading is more than one level deeper than the heading before it.</summary>
    HeadingLevelSkipped = 13,

    /// <summary>The document has more than one level 1 heading.</summary>
    MultipleLevelOneHeadings = 14,

    /// <summary>A figure has neither <c>/Alt</c> nor <c>/ActualText</c>.</summary>
    FigureNoDescription = 15,

    /// <summary>A table has no header cells.</summary>
    TableNoHeaderCells = 16,

    /// <summary>A table has header cells but none states a scope or lists the headers of a cell.</summary>
    TableHeaderNoScope = 17,

    /// <summary>A table cell is not labelled by any header cell.</summary>
    TableCellNoHeader = 18,

    /// <summary>A list item is not inside a list.</summary>
    ListItemOutsideList = 19,

    /// <summary>A link annotation is not inside a Link structure element.</summary>
    LinkNotTagged = 20,

    /// <summary>A link has no <c>/Contents</c> and its structure element has no <c>/Alt</c>.</summary>
    LinkNoDescription = 21,

    /// <summary>A form field has no <c>/TU</c> label.</summary>
    FormFieldNoLabel = 22,

    /// <summary>A form field is not inside a Form structure element.</summary>
    FormFieldNotTagged = 23,

    /// <summary>An annotation is not referred to from the structure tree.</summary>
    AnnotationNotTagged = 24,

    /// <summary>An annotation is tagged with a type other than Annot (PDF/UA-1).</summary>
    AnnotationWrongStructureType = 25,
}
