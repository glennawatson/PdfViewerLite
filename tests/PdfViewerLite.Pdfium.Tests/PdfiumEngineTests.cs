// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Platform;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for <see cref="PdfiumEngine"/>.</summary>
public sealed class PdfiumEngineTests
{
    /// <summary>The pages in the test documents.</summary>
    private const int Pages = 3;

    /// <summary>Verifies a missing file reports a file error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingFileReportsFileError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"missing-{Guid.NewGuid():N}.pdf");

        var exception = await Assert.That(() => new PdfiumEngine().Open(path, null)).Throws<DocumentOpenException>();

        await Assert.That(exception!.Error).IsEqualTo(DocumentOpenError.File);
    }

    /// <summary>Verifies garbage reports a format error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GarbageReportsFormatError()
    {
        var path = Path.Combine(Path.GetTempPath(), $"garbage-{Guid.NewGuid():N}.pdf");
        await File.WriteAllTextAsync(path, "this is not a pdf");
        try
        {
            var exception = await Assert.That(() => new PdfiumEngine().Open(path, null)).Throws<DocumentOpenException>();

            await Assert.That(exception!.Error).IsEqualTo(DocumentOpenError.Format);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies PDFs are recognised by extension or signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesPdfFiles()
    {
        var engine = new PdfiumEngine();
        var withoutExtension = Path.Combine(Path.GetTempPath(), $"noext-{Guid.NewGuid():N}");
        await File.WriteAllBytesAsync(withoutExtension, TestPdf.Create(1));
        try
        {
            await Assert.That(engine.CanOpen("/does/not/matter.PDF")).IsTrue();
            await Assert.That(engine.CanOpen(withoutExtension)).IsTrue();
            await Assert.That(engine.CanOpen("/missing/file.txt")).IsFalse();
        }
        finally
        {
            File.Delete(withoutExtension);
        }
    }

    /// <summary>A path with accented letters opens on every platform, since .NET opens the file rather than PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpensPathsWithAccents()
    {
        var folder = Path.Combine(Path.GetTempPath(), $"résumé-ü-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(folder);
        var path = Path.Combine(folder, "naïve café.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages));
        try
        {
            using var document = new PdfiumEngine().Open(path, null);

            await Assert.That(document.PageCount).IsEqualTo(Pages);
            await Assert.That(document.GetText(Pages - 1, 0, TestPdf.Sentence.Length)).StartsWith("Page 3");
        }
        finally
        {
            Directory.Delete(folder, true);
        }
    }

    /// <summary>
    /// Saving moves a new file over the open one; the open document keeps reading its own file afterwards, Windows
    /// included, where the file could not be replaced when PDFium opened it itself.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KeepsReadingAfterTheFileIsReplaced()
    {
        var path = Path.Combine(Path.GetTempPath(), $"replace-{Guid.NewGuid():N}.pdf");
        var replacement = $"{path}.saving";
        await File.WriteAllBytesAsync(path, TestPdf.Create(Pages));
        await File.WriteAllBytesAsync(replacement, TestPdf.Create(1));
        try
        {
            using var document = new PdfiumEngine().Open(path, null);

            FileReplacement.Replace(replacement, path);

            await Assert.That(document.GetText(Pages - 1, 0, TestPdf.Sentence.Length)).StartsWith("Page 3");
            using var reopened = new PdfiumEngine().Open(path, null);
            await Assert.That(reopened.PageCount).IsEqualTo(1);
        }
        finally
        {
            File.Delete(path);
        }
    }
}
