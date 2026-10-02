// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for <see cref="PdfiumEngine"/>.</summary>
public sealed class PdfiumEngineTests
{
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
}
