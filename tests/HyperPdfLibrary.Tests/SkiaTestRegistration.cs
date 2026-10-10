// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;

namespace HyperPdfLibrary.Tests;

/// <summary>Registers Skia drawing services for direct core tests.</summary>
internal static class SkiaTestRegistration
{
    /// <summary>Registers the drawing backend used by rendering tests.</summary>
    [ModuleInitializer]
    internal static void Register() => PdfDrawingServices.Register(new PdfRenderBackendRegistration().UseSkia());
}
