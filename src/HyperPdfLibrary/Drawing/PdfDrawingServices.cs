// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>Stores explicitly registered drawing services for the process.</summary>
public static class PdfDrawingServices
{
    /// <summary>The configured immutable registration.</summary>
    private static PdfRenderBackendRegistration _registration = new();

    /// <summary>Gets the optional font provider.</summary>
    public static IPdfFontProvider? Fonts => Volatile.Read(ref _registration).Fonts;

    /// <summary>Gets the optional image codec.</summary>
    public static IPdfImageCodec? Images => Volatile.Read(ref _registration).Images;

    /// <summary>Gets the registered drawing backend.</summary>
    /// <exception cref="InvalidOperationException">No drawing backend has been registered.</exception>
    public static IPdfRenderBackend Backend => PdfDrawingServices.ConfiguredBackend
        ?? throw new InvalidOperationException("Register a drawing backend before rendering PDF pages.");

    /// <summary>Gets the optional drawing backend for rendering resources.</summary>
    internal static IPdfRenderBackend? ConfiguredBackend => Volatile.Read(ref _registration).Backend;

    /// <summary>Registers drawing services before opening documents that use them.</summary>
    /// <param name="registration">The immutable service registration.</param>
    public static void Register(PdfRenderBackendRegistration registration)
    {
        ArgumentNullException.ThrowIfNull(registration);
        Volatile.Write(ref _registration, registration);
    }
}
