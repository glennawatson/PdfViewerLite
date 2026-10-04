// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Signatures;
using PdfViewerLite.TestAssets;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Tests for <see cref="SignaturesViewModel"/> and placing signatures through Fill &amp; Sign.</summary>
public sealed class SignatureViewModelTests
{
    /// <summary>The page count of the signed document.</summary>
    private const int Pages = 2;

    /// <summary>Where the signature is placed.</summary>
    private static readonly PdfViewerLite.Core.Geometry.PagePoint SignAt = new(72, 500);

    /// <summary>Verifies a signed document reports its signature, and checking shows it with its signer.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksSignaturesWhenAsked()
    {
        using var test = new TestServices();
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var path = Path.Combine(test.Directory, "signed.pdf");
        await File.WriteAllBytesAsync(path, TestSignedPdf.Create(Pages, certificate));
        using var main = new MainViewModel(test.Services);
        main.Open([path]);
        var signatures = main.SelectedTab!.Signatures;
        var shown = 0;
        using var show = signatures.ShowInteraction.RegisterHandler(context =>
        {
            shown++;
            context.SetOutput(RxVoid.Default);
        });

        var before = signatures.Signatures.Count;
        _ = await signatures.CheckCommand.Execute().ToTask();

        await Assert.That(signatures.SignatureCount).IsEqualTo(1);
        await Assert.That(before).IsEqualTo(0);
        await Assert.That(shown).IsEqualTo(1);
        await Assert.That(signatures.Signatures[0].SignerName).IsEqualTo(TestSignedPdf.SignerName);
        await Assert.That(signatures.Signatures[0].Integrity).IsEqualTo(SignatureIntegrity.Intact);
    }

    /// <summary>Verifies typing a signature remembers the name and places it with one click.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlacesTypedSignature()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("sign.pdf", Pages)]);
        var tab = main.SelectedTab!;
        using var prompt = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("Glenn Watson"));
        tab.SidebarMode = SidebarMode.Annotations;

        _ = await tab.FillAndSign.StartCommand.Execute().ToTask();
        _ = await tab.FillAndSign.TypeSignatureCommand.Execute().ToTask();
        var armed = tab.Annotations.Tool;
        tab.FillAndSign.PlaceSignature(0, SignAt);

        await Assert.That(armed).IsEqualTo(AnnotationTool.PlaceSignature);
        await Assert.That(test.Services.Settings.SignatureName).IsEqualTo("Glenn Watson");
        await Assert.That(tab.Annotations.Tool).IsEqualTo(AnnotationTool.Select);
        await Assert.That(tab.Annotations.Items.Single().Annotation.Kind).IsEqualTo(PdfViewerLite.Core.Annotations.AnnotationKind.Signature);
    }
}
