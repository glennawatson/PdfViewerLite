// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.Pdfium.Tests;

/// <summary>Tests finding content PDFium cannot show or run.</summary>
public sealed class ContentCheckTests
{
    /// <summary>Verifies XFA forms, scripts, media players and 3D models are found, and a plain document has none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FindsUnsupportedContent()
    {
        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-unsupported-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        await File.WriteAllBytesAsync(path, TestPdf.CreateWithUnsupportedContent());
        try
        {
            using var document = new PdfiumEngine().Open(path, null);
            var check = (IContentCheck)document;
            await Assert.That(check.CheckDocument()).IsEqualTo(UnsupportedContent.XfaForm | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(0)).IsEqualTo(UnsupportedContent.Multimedia | UnsupportedContent.ThreeD | UnsupportedContent.JavaScript);
            await Assert.That(check.CheckPage(1)).IsEqualTo(UnsupportedContent.None);

            using var simple = new PdfiumEngine().Open(plain, null);
            await Assert.That(((IContentCheck)simple).CheckDocument()).IsEqualTo(UnsupportedContent.None);
            await Assert.That(((IContentCheck)simple).CheckPage(0)).IsEqualTo(UnsupportedContent.None);
        }
        finally
        {
            File.Delete(path);
            File.Delete(plain);
        }
    }
}
