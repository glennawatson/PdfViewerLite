// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests for reading embedded files through <see cref="IAttachmentSource"/>.</summary>
public sealed class AttachmentTests
{
    /// <summary>Verifies the embedded file is listed with its name and size, and its contents can be saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListsAndSavesAttachment()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-attach-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithAttachment());
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var source = (IAttachmentSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAttachmentSource))!;
            var attachments = source.GetAttachments();
            await using var saved = new MemoryStream();
            var ok = source.SaveAttachment(attachments[0].Index, saved);

            await Assert.That(attachments.Count).IsEqualTo(1);
            await Assert.That(attachments[0].Name).IsEqualTo(TestPdf.AttachmentName);
            await Assert.That(attachments[0].Size).IsEqualTo(TestPdf.AttachmentText.Length);
            await Assert.That(ok).IsTrue();
            await Assert.That(Encoding.ASCII.GetString(saved.ToArray())).IsEqualTo(TestPdf.AttachmentText);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Verifies a document without attachments lists none and refuses to save one.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoAttachments()
    {
        var path = TestPdf.WriteTempFile(1);
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var source = (IAttachmentSource)PdfViewerLite.Core.Documents.DocumentFeatures.CastFeature(document, typeof(IAttachmentSource))!;
            await using var saved = new MemoryStream();

            await Assert.That(source.GetAttachments().Count).IsEqualTo(0);
            await Assert.That(source.SaveAttachment(0, saved)).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
