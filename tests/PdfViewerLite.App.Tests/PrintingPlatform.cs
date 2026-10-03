// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.App.Tests;

/// <summary>A desktop with nothing but a print service.</summary>
/// <param name="printer">The print service.</param>
internal sealed class PrintingPlatform(IPrintService printer) : IDesktopPlatform
{
    /// <summary>The integration printing is added to.</summary>
    private readonly FallbackPlatform _fallback = new();

    /// <inheritdoc/>
    public string Name => "Test";

    /// <inheritdoc/>
    public IDesktopThemeSource? ThemeSource => null;

    /// <inheritdoc/>
    public IFileManagerLauncher FileManager => _fallback.FileManager;

    /// <inheritdoc/>
    public IRecentDocumentStore RecentDocuments => _fallback.RecentDocuments;

    /// <inheritdoc/>
    public IPrintService Printer => printer;

    /// <inheritdoc/>
    public string? GetLaunchActivationToken() => null;

    /// <inheritdoc/>
    public PdfViewerLite.Core.Speech.IAudioOutput CreateAudioOutput() => new PdfViewerLite.Core.Speech.NullAudioOutput();

    /// <inheritdoc/>
    public Task<bool> TryForwardAsync(OpenRequest request) => Task.FromResult(false);

    /// <inheritdoc/>
    public Task<ISingleInstance?> TryClaimSingleInstanceAsync() => Task.FromResult<ISingleInstance?>(null);
}
