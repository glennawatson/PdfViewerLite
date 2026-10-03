// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Platform;
using PdfViewerLite.Platform.Linux;

namespace PdfViewerLite.App.Services;

/// <summary>Chooses the desktop integration for the platform the app runs on.</summary>
public static class DesktopPlatforms
{
    /// <summary>Gets the integration for this platform.</summary>
    /// <returns>KDE Plasma (and the freedesktop parts) on Linux and FreeBSD; otherwise none.</returns>
    public static IDesktopPlatform Detect() =>
        OperatingSystem.IsLinux() || OperatingSystem.IsFreeBSD() ? new KdePlatform() : new FallbackPlatform();
}
