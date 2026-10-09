// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Reads the conformance claims of cached corpus files; each test does nothing when its file is not cached.</summary>
public sealed class ConformanceCorpusTests
{
    /// <summary>The PDF/A file from the pypdf sample set.</summary>
    private const string PypdfPdfA = "pypdf-pdfa.pdf";

    /// <summary>The PDF/UA and PDF/A file from the pyHanko test data.</summary>
    private const string PyhankoUaAndA = "pyhanko-ua-and-a.pdf";

    /// <summary>The pypdf PDF/A sample claims a PDF/A part.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PypdfPdfAGivesAClaim()
    {
        if (Read(PypdfPdfA) is not { } report)
        {
            return;
        }

        await Assert.That(report.PdfA).IsNotNull();
        await Assert.That(report.PdfA!.IsRecognised).IsTrue();
        await Assert.That(report.OutputIntents.Length).IsGreaterThan(0);
    }

    /// <summary>The pyHanko sample claims PDF/A and PDF/UA.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PyhankoUaAndAGivesBothClaims()
    {
        if (Read(PyhankoUaAndA) is not { } report)
        {
            return;
        }

        await Assert.That(report.PdfA).IsNotNull();
        await Assert.That(report.PdfUaPart).IsNotNull();
    }

    /// <summary>Reads the report of a cached corpus file.</summary>
    /// <param name="name">The file name.</param>
    /// <returns>The report, or null when the file is not cached.</returns>
    private static HyperPdfLibrary.Conformance.PdfConformanceReport? Read(string name)
    {
        var path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.UserProfile), ".cache", "pdfviewerlite", "corpus", name);
        if (!File.Exists(path))
        {
            return null;
        }

        using var document = PdfDocument.Open(File.ReadAllBytes(path), null);
        return document.GetConformance();
    }
}
