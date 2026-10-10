// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Registers Skia drawing services for managed-engine tests.</summary>
internal static class SkiaTestRegistration
{
    /// <summary>Registers the drawing backend used by managed-engine and parity tests.</summary>
    [ModuleInitializer]
    internal static void Register() => PdfDrawingServices.Register(new PdfRenderBackendRegistration().UseSkia());
}
