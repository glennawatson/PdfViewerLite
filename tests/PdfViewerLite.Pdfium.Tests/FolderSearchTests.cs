// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Search;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Checks searching every PDF in a folder.</summary>
public sealed class FolderSearchTests
{
    /// <summary>The pages in each test document.</summary>
    private const int Pages = 3;

    /// <summary>The words searched for.</summary>
    private const string Words = "brown fox";

    /// <summary>The PDFs at the top of the test folder.</summary>
    private const int TopLevelFiles = 2;

    /// <summary>The PDFs in the test folder and its subfolder.</summary>
    private const int AllFiles = 3;

    /// <summary>The most matches kept per file in the tests.</summary>
    private const int MaxMatches = 100;

    /// <summary>The PDFs in a folder are found, in subfolders only when asked, and other files are ignored.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsThePdfFiles()
    {
        using var folder = new TestFolder();

        var shallow = FolderSearch.FindFiles(folder.Path, false);
        var deep = FolderSearch.FindFiles(folder.Path, true);

        await Assert.That(shallow.Count).IsEqualTo(TopLevelFiles);
        await Assert.That(deep.Count).IsEqualTo(AllFiles);
        await Assert.That(deep.TrueForAll(static f => f.EndsWith(".pdf", StringComparison.OrdinalIgnoreCase))).IsTrue();
        await Assert.That(FolderSearch.FindFiles(Path.Combine(folder.Path, "missing"), true).Count).IsEqualTo(0);
    }

    /// <summary>Each match comes with its page and the words around it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsTheWordsWithTheirContext()
    {
        using var folder = new TestFolder();
        var file = FolderSearch.FindFiles(folder.Path, false)[0];

        var found = FolderSearch.SearchFile(new PdfiumEngine(), file, Words, SearchOptions.None, MaxMatches, CancellationToken.None);
        var first = found.Matches[0];

        await Assert.That(found.Problem).IsNull();
        await Assert.That(found.Matches.Count).IsEqualTo(Pages);
        await Assert.That(found.Matches.Select(static m => m.PageIndex)).IsEquivalentTo(Enumerable.Range(0, Pages));
        await Assert.That(first.Snippet.Substring(first.MatchStart, first.MatchLength)).IsEqualTo(Words);
        await Assert.That(first.Snippet).Contains("quick brown fox jumps");
    }

    /// <summary>A file that cannot be opened is reported, and a file with many matches is cut short.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsProblemsAndLimits()
    {
        using var folder = new TestFolder();
        var engine = new PdfiumEngine();

        var broken = FolderSearch.SearchFile(engine, folder.WriteBroken(), "fox", SearchOptions.None, MaxMatches, CancellationToken.None);
        var limited = FolderSearch.SearchFile(engine, FolderSearch.FindFiles(folder.Path, false)[1], "fox", SearchOptions.None, 1, CancellationToken.None);

        await Assert.That(broken.Problem).IsEqualTo("could not be opened");
        await Assert.That(limited.Matches.Count).IsEqualTo(1);
        await Assert.That(limited.IsTruncated).IsTrue();
    }

    /// <summary>A folder of test documents: two PDFs and a text file at the top, and one PDF in a subfolder.</summary>
    private sealed class TestFolder : IDisposable
    {
        /// <summary>Initializes a new instance of the <see cref="TestFolder"/> class.</summary>
        internal TestFolder()
        {
            Path = System.IO.Path.Combine(System.IO.Path.GetTempPath(), $"pdfviewerlite-folder-{Guid.NewGuid():N}");
            var sub = Directory.CreateDirectory(System.IO.Path.Combine(Path, "sub")).FullName;
            File.WriteAllBytes(System.IO.Path.Combine(Path, "a.pdf"), TestPdf.Create(Pages));
            File.WriteAllBytes(System.IO.Path.Combine(Path, "b.PDF"), TestPdf.Create(Pages));
            File.WriteAllBytes(System.IO.Path.Combine(sub, "c.pdf"), TestPdf.Create(Pages));
            File.WriteAllText(System.IO.Path.Combine(Path, "notes.txt"), Words);
        }

        /// <summary>Gets the folder.</summary>
        internal string Path { get; }

        /// <inheritdoc/>
        public void Dispose()
        {
            Directory.Delete(Path, true);
            File.Delete($"{Path}-broken.pdf");
        }

        /// <summary>Writes a file that is called a PDF but is not one, beside the folder.</summary>
        /// <returns>Its path.</returns>
        internal string WriteBroken()
        {
            var path = $"{Path}-broken.pdf";
            File.WriteAllText(path, "not a pdf");
            return path;
        }
    }
}
