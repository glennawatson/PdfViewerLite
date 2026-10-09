// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using PdfViewerLite.TestAssets;

namespace HyperPdfLibrary.Tests.Security;

/// <summary>Tests for unencrypted wrapper documents (ISO 32000-2 7.6.7).</summary>
public sealed class WrapperDocumentTests
{
    /// <summary>The payload's file name.</summary>
    private const string PayloadName = "payload.pdf";

    /// <summary>The cryptographic filter the payload names.</summary>
    private const string FilterName = "MicrosoftIRMServices";

    /// <summary>A wrapper's payload is found with its file name, cryptographic filter and version.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PayloadIsListed()
    {
        using var document = PdfDocument.Open(Wrapper(), null);
        var payload = document.GetEncryptedPayload();

        await Assert.That(document.IsWrapperDocument).IsTrue();
        await Assert.That(payload).IsNotNull();
        await Assert.That(payload!.FileName).IsEqualTo(PayloadName);
        await Assert.That(payload.CryptographicFilter).IsEqualTo(FilterName);
        await Assert.That(payload.Version).IsEqualTo("2");
        await Assert.That(payload.GetBytes()).IsEquivalentTo(Payload());
    }

    /// <summary>The payload opens as a document of its own.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PayloadOpens()
    {
        using var document = PdfDocument.Open(Wrapper(), null);
        using var payload = document.OpenEncryptedPayload(null);

        await Assert.That(payload.PageCount).IsEqualTo(1);
    }

    /// <summary>A plain document is not a wrapper.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainDocumentIsNotAWrapper()
    {
        using var document = PdfDocument.Open(Payload(), null);

        await Assert.That(document.IsWrapperDocument).IsFalse();
        await Assert.That(() => document.OpenEncryptedPayload(null)).Throws<PdfException>();
    }

    /// <summary>Builds the payload: a one-page document.</summary>
    /// <returns>The file.</returns>
    private static byte[] Payload() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 100 100] >>");

    /// <summary>Builds a wrapper: a collection whose associated file is the payload.</summary>
    /// <returns>The file.</returns>
    private static byte[] Wrapper() => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R /Collection << /View /H >> /AF [4 0 R] /Names << /EmbeddedFiles << /Names [(payload.pdf) 4 0 R] >> >> >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>",
        $"<< /Type /Filespec /F ({PayloadName}) /UF ({PayloadName}) /AFRelationship /EncryptedPayload /EP << /Type /EncryptedPayload /Subtype /{FilterName} /Version (2) >> /EF << /F 5 0 R >> >>",
        MiniPdf.Stream("/Type /EmbeddedFile", Encoding.Latin1.GetString(Payload())));
}
