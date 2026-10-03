// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Settings;
using PdfViewerLite.Pdfium;
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
    {
        Directory = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-app-{Guid.NewGuid():N}");
        _ = System.IO.Directory.CreateDirectory(Directory);
        Services = new(new SettingsStore(Path.Combine(Directory, "settings.json")), new PdfiumEngine(), platform);
    }

    /// <summary>Gets the working directory.</summary>
    internal string Directory { get; }

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
}
