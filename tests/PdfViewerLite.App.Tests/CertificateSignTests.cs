// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography.X509Certificates;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for signing with a certificate from the Fill &amp; Sign tools.</summary>
public sealed class CertificateSignTests
{
    /// <summary>The certificate file's password.</summary>
    private const string Password = "correct horse";

    /// <summary>The pages in the document.</summary>
    private const int Pages = 2;

    /// <summary>Verifies signing writes a signed copy, remembers the certificate and opens the copy with its signature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SignsCopyAndOpensIt()
    {
        using var test = new TestServices();
        var certificatePath = WriteCertificate(test.Directory);
        var destination = Path.Combine(test.Directory, "signed.pdf");
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("contract.pdf", Pages)]);
        var tab = main.SelectedTab!;
        string? suggested = null;
        using var sign = tab.Signatures.CertificateSignInteraction.RegisterHandler(context =>
        {
            context.Input.CertificatePath = certificatePath;
            context.Input.Password = Password;
            context.Input.Reason = "Agreed";
            _ = context.Input.SignCommand.Execute().Subscribe();
            context.SetOutput(context.Input.Error is null);
        });
        using var save = tab.SaveAsInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(destination);
        });

        _ = await tab.Signatures.SignWithCertificateCommand.Execute().ToTask();
        var opened = main.Tabs.FirstOrDefault(t => t.FilePath == destination);
        opened?.EnsureLoaded();
        opened?.Signatures.Refresh();

        await Assert.That(suggested).IsEqualTo("contract-signed.pdf");
        await Assert.That(tab.Notice).IsEqualTo("Signed copy saved as signed.pdf.");
        await Assert.That(test.Services.Settings.SigningCertificatePath).IsEqualTo(certificatePath);
        await Assert.That(opened).IsNotNull();
        await Assert.That(opened!.Signatures.SignatureCount).IsEqualTo(1);
    }

    /// <summary>Verifies a wrong password is explained in the window instead of signing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExplainsWrongPassword()
    {
        using var test = new TestServices();
        using var request = new CertificateSignViewModel(WriteCertificate(test.Directory)) { Password = "wrong" };

        _ = await request.SignCommand.Execute().ToTask();

        await Assert.That(request.Error).IsEqualTo("The password is not right, or the file is not a certificate.");
        await Assert.That(request.TakeCertificate()).IsNull();
    }

    /// <summary>Writes a test certificate with its key to a password protected .pfx file.</summary>
    /// <param name="directory">Where to write it.</param>
    /// <returns>The file.</returns>
    private static string WriteCertificate(string directory)
    {
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Path.Combine(directory, "signer.pfx");
        File.WriteAllBytes(path, certificate.Export(X509ContentType.Pkcs12, Password));
        return path;
    }
}
