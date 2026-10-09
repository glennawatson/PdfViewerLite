// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.App.Tests;

/// <summary>Application services isolated in a temporary directory, with generated documents.</summary>
internal sealed class TestServices : IDisposable
{
    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    internal TestServices()
        : this(new FallbackPlatform())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a desktop integration.</summary>
    /// <param name="platform">The desktop integration.</param>
    internal TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform)
        : this(platform, new FakeSpeech(true, false))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a desktop integration and a fake voice.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    internal TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech)
        : this(platform, speech, null)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class with a fake text recogniser.</summary>
    /// <param name="ocr">The fake recogniser and language packs.</param>
    internal TestServices(FakeOcr ocr)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), ocr)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class that opens documents with one engine.</summary>
    /// <param name="engine">The engine every document opens with.</param>
    internal TestServices(PdfEngineChoice engine)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), null, new SelectableDocumentEngine(() => engine))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class that opens documents with an engine directly, whatever the engine override says.</summary>
    /// <param name="engine">The engine every document opens with.</param>
    internal TestServices(IDocumentEngine engine)
        : this(new FallbackPlatform(), new FakeSpeech(true, false), null, engine)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    /// <param name="ocr">
    /// The fake recogniser, or <see langword="null"/> for the Tesseract and English data shipped with the app, with an
    /// empty pack folder in the test folder and downloads never fetched from the network.
    /// </param>
    /// <remarks>
    /// Documents open with PDFium unless <c>PDFVIEWERLITE_ENGINE</c> names another engine, so setting
    /// <c>PDFVIEWERLITE_ENGINE=hyperpdf</c> runs the whole suite on HyperPDF.
    /// </remarks>
    private TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech, FakeOcr? ocr)
        : this(platform, speech, ocr, new SelectableDocumentEngine(static () => PdfEngineChoice.Pdfium))
    {
    }

    /// <summary>Initializes a new instance of the <see cref="TestServices"/> class.</summary>
    /// <param name="platform">The desktop integration.</param>
    /// <param name="speech">The fake voice and sound output.</param>
    /// <param name="ocr">The fake recogniser, or <see langword="null"/> for the shipped one.</param>
    /// <param name="engine">The engine documents open with.</param>
    private TestServices(PdfViewerLite.Core.Platform.IDesktopPlatform platform, FakeSpeech speech, FakeOcr? ocr, IDocumentEngine engine)
    {
        Speech = speech;
        Directory = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-app-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(Directory);
        var packs = Path.Combine(Directory, "tessdata");
        Services = new(new SettingsStore(Path.Combine(Directory, "settings.json")), engine, platform)
        {
            Speech = speech.CreateSetup(Directory),
            Ocr = ocr?.CreateSetup(packs) ?? OcrSetup.CreateDefault() with { LanguageDirectory = packs, DownloadPacks = new FakeOcr(true).DownloadAsync },
            CreateSpellChecker = static () => FakeSpellChecker.Instance,
        };
    }

    /// <summary>Gets the working directory.</summary>
    internal string Directory { get; }

    /// <summary>Gets the fake voice and sound output.</summary>
    internal FakeSpeech Speech { get; }

    /// <summary>Gets the services.</summary>
    internal AppServices Services { get; }

    /// <inheritdoc/>
    public void Dispose()
    {
        Services.Dispose();
        System.IO.Directory.Delete(Directory, true);
    }

    /// <summary>Writes a generated document.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="pageCount">The page count.</param>
    /// <returns>The path.</returns>
    internal string CreateDocument(string name, int pageCount)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllBytes(path, TestPdf.Create(pageCount));
        return path;
    }

    /// <summary>Writes a document from its bytes.</summary>
    /// <param name="name">The file name.</param>
    /// <param name="bytes">The PDF.</param>
    /// <returns>The path.</returns>
    internal string CreateDocument(string name, byte[] bytes)
    {
        var path = Path.Combine(Directory, name);
        File.WriteAllBytes(path, bytes);
        return path;
    }
}
