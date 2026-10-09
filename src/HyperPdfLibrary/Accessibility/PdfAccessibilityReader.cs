// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Builds a document's <see cref="PdfAccessibilityReport"/>: document entries first, then the structure tree, then each page.</summary>
internal static class PdfAccessibilityReader
{
    /// <summary>The first PDF version number that is PDF 2.0.</summary>
    private const char FirstPdf2Digit = '2';

    /// <summary>Reads a document's accessibility.</summary>
    /// <param name="document">The document.</param>
    /// <param name="cancellation">The cancellation token, checked on every element and page.</param>
    /// <returns>The report.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static PdfAccessibilityReport Read(PdfDocument document, CancellationToken cancellation)
    {
        var findings = new AccessibilityFindings();
        var xmp = PdfDocumentMetadata.GetXmp(document);
        var tree = PdfDocumentTagged.GetStructureTree(document);
        var markInfo = document.Catalog.GetDictionary(KnownName.MarkInfo);
        var part = xmp?.PdfUaPart;
        var scan = tree is null ? null : new AccessibilityStructureScan(tree, part, findings, cancellation);

        var isMarked = markInfo?.Flag("Marked", false) == true;
        var language = document.Catalog.GetText(KnownName.Lang) is { Length: > 0 } text ? text : null;
        var hasTitle = !string.IsNullOrWhiteSpace(xmp?.Title);
        var displayTitle = PdfDocumentViewerPreferences.GetViewerPreferences(document).DisplayDocTitle;
        var suspects = markInfo?.Flag("Suspects", false) == true;
        var version = PdfDocumentMetadata.GetInfo(document).Version ?? string.Empty;

        CheckDocument(findings, new(isMarked, tree is not null, language is not null, hasTitle, displayTitle, suspects), part, version);
        scan?.Run();
        var pages = CheckPages(document, scan, part, findings, cancellation);
        var claim = new PdfUaClaim(part, ReadRevision(xmp), version, UsesPdf20Namespaces(tree, scan));
        return new(claim, isMarked, tree is not null, language, hasTitle, displayTitle, suspects, pages, findings.Findings, findings.GetCounts());
    }

    /// <summary>Reads <c>pdfuaid:rev</c>.</summary>
    /// <param name="xmp">The metadata, or <see langword="null"/>.</param>
    /// <returns>The revision, or <see langword="null"/> when missing or empty.</returns>
    private static string? ReadRevision(XmpMetadata? xmp) =>
        xmp?.GetValue(XmpMetadata.PdfUaIdNamespace, "rev") is { Length: > 0 } revision ? revision : null;

    /// <summary>Determines whether a version string names PDF 2.0 or later.</summary>
    /// <param name="version">The version, for example "1.7", "2.0" or "PDF-2.0".</param>
    /// <returns><see langword="true"/> for PDF 2.0 or later.</returns>
    private static bool IsPdf2OrLater(string version)
    {
        foreach (var character in version)
        {
            if (char.IsAsciiDigit(character))
            {
                return character >= FirstPdf2Digit;
            }
        }

        return false;
    }

    /// <summary>Determines whether the tree lists namespaces or uses the PDF 2.0 one.</summary>
    /// <param name="tree">The tree, or <see langword="null"/>.</param>
    /// <param name="scan">The structure walk, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool UsesPdf20Namespaces(PdfStructureTree? tree, AccessibilityStructureScan? scan) =>
        scan?.UsesPdf20Namespace == true || tree?.Root.Array("Namespaces") is { Count: > 0 };

    /// <summary>Reports what the catalog, the metadata and the claim say.</summary>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="state">What the document entries say.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="version">The file's version.</param>
    private static void CheckDocument(AccessibilityFindings findings, in DocumentState state, int? part, string version)
    {
        AddIf(findings, !state.IsMarked, PdfAccessibilityCode.NotMarked);
        AddIf(findings, !state.HasStructureTree, PdfAccessibilityCode.NoStructureTree);
        AddIf(findings, !state.HasLanguage, PdfAccessibilityCode.NoLanguage);
        AddIf(findings, !state.HasTitle, PdfAccessibilityCode.NoTitle);
        AddIf(findings, !state.DisplayDocTitle, PdfAccessibilityCode.DisplayDocTitleOff);
        AddIf(findings, state.HasSuspects, PdfAccessibilityCode.SuspectsSet);
        AddIf(findings, part is not null and not PdfUaClaim.Part1 and not PdfUaClaim.Part2, PdfAccessibilityCode.ClaimPartUnknown);
        AddIf(findings, part == PdfUaClaim.Part2 && !IsPdf2OrLater(version), PdfAccessibilityCode.ClaimPart2NotPdf2);
    }

    /// <summary>Adds a document-level finding when a condition holds.</summary>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="condition">Whether to add it.</param>
    /// <param name="code">The code.</param>
    private static void AddIf(AccessibilityFindings findings, bool condition, PdfAccessibilityCode code)
    {
        if (condition)
        {
            findings.Add(code);
        }
    }

    /// <summary>Checks every page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="scan">The structure walk, or <see langword="null"/> for an untagged document.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellation">The cancellation token.</param>
    /// <returns>What was counted on each page.</returns>
    private static PdfAccessibilityPage[] CheckPages(PdfDocument document, AccessibilityStructureScan? scan, int? part, AccessibilityFindings findings, CancellationToken cancellation)
    {
        var pageScan = new AccessibilityPageScan(document, scan, part, findings);
        var pages = new PdfAccessibilityPage[document.PageCount];
        for (var i = 0; i < pages.Length; i++)
        {
            cancellation.ThrowIfCancellationRequested();
            pages[i] = pageScan.Check(i);
        }

        return pages;
    }

    /// <summary>What the document's own entries say.</summary>
    /// <param name="IsMarked">Whether <c>/MarkInfo /Marked</c> is true.</param>
    /// <param name="HasStructureTree">Whether there is a structure tree.</param>
    /// <param name="HasLanguage">Whether the catalog has a language.</param>
    /// <param name="HasTitle">Whether the metadata has a title.</param>
    /// <param name="DisplayDocTitle">Whether the viewer preferences show the title.</param>
    /// <param name="HasSuspects">Whether <c>/Suspects</c> is true.</param>
    private readonly record struct DocumentState(bool IsMarked, bool HasStructureTree, bool HasLanguage, bool HasTitle, bool DisplayDocTitle, bool HasSuspects);
}
