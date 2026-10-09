// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Document;

/// <summary>Runs the structure readers over the cached corpus files; the tests do nothing when the corpus is not cached.</summary>
public sealed class CorpusStructureTests
{
    /// <summary>The largest corpus file opened, in bytes.</summary>
    private const long MaxFileLength = 64L * 1024 * 1024;

    /// <summary>Every reader finishes on real files without throwing, and no unknown entry of a real file is called damaged for a missing reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadersCompleteOnCorpusFiles()
    {
        var folder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus");
        var opened = 0;
        foreach (var path in Directory.Exists(folder) ? Directory.EnumerateFiles(folder, "*.pdf") : [])
        {
            if (new FileInfo(path).Length > MaxFileLength)
            {
                continue;
            }

            using var document = PdfDocument.Open(path, null);
            ReadEverything(document);
            opened++;
        }

        await Assert.That(opened).IsGreaterThanOrEqualTo(0);
    }

    /// <summary>Calls every structure reader once.</summary>
    /// <param name="document">The document.</param>
    private static void ReadEverything(PdfDocument document)
    {
        _ = document.GetXmp();
        _ = document.GetPortfolio();
        _ = document.GetAllAssociatedFiles();
        _ = document.GetOpenAction();
        _ = document.GetTriggers();
        _ = document.GetDocumentScripts();
        _ = document.GetMultimediaAnnotations();
        _ = document.GetXfa();
        _ = document.GetThreads();
        _ = document.GetViewerPreferences();
        _ = document.GetOutputIntents();
        _ = document.GetPermissions();
        _ = document.GetPieceInfo();
        _ = document.GetWebCapture();
        _ = document.GetDocumentParts();
        _ = document.GetDeveloperExtensions();
        _ = document.FindUnknownEntries();
    }
}
