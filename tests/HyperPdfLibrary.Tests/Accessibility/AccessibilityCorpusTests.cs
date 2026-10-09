// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>
/// Reads the PDF/UA-1 corpus files and checks the report. The expectations come from reading the files: each claims
/// part 1, is tagged, has a language and title, and draws no loose text. The tests do nothing when a file is not cached.
/// </summary>
[NotInParallel]
public sealed class AccessibilityCorpusTests
{
    /// <summary>The corpus file ids.</summary>
    private static readonly string[] Ids = ["verapdf-ua-headings", "verapdf-ua-tables", "verapdf-ua-notes", "pyhanko-ua-and-a"];

    /// <summary>The UA-1 corpus files claim part 1, are tagged and have no findings on the rules covered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Ua1FilesClaimPart1WithNoFindings()
    {
        foreach (var id in Ids)
        {
            if (Open(id) is not { } document)
            {
                continue;
            }

            using (document)
            {
                var report = document.GetAccessibilityReport();

                await Assert.That(report.Claim.Part).IsEqualTo(1);
                await Assert.That(report.IsMarked).IsTrue();
                await Assert.That(report.HasStructureTree).IsTrue();
                await Assert.That(report.Language).IsNotNull();
                await Assert.That(report.HasTitle).IsTrue();
                await Assert.That(report.DisplayDocTitle).IsTrue();
                await Assert.That(report.Findings.Count).IsEqualTo(0);
            }
        }
    }

    /// <summary>The notes file has one annotation and its page follows structure order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesFileHasOneAnnotationWithStructureTabs()
    {
        if (Open("verapdf-ua-notes") is not { } document)
        {
            return;
        }

        using (document)
        {
            var page = document.GetAccessibilityReport().Pages[0];

            await Assert.That(page.AnnotationCount).IsEqualTo(1);
            await Assert.That(page.TabsFollowStructure).IsTrue();
        }
    }

    /// <summary>Opens a cached corpus file.</summary>
    /// <param name="id">The corpus id.</param>
    /// <returns>The document, or <see langword="null"/> when the file is not cached.</returns>
    private static PdfDocument? Open(string id)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus", $"{id}.pdf");
        return File.Exists(path) ? PdfDocument.Open(path, null) : null;
    }
}
