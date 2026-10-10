// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia.Fonts;
using HyperPdfLibrary.Render.Skia.Images;

namespace HyperPdfLibrary.Render.Skia;

/// <summary>Selects Skia rendering, platform fonts and image codecs explicitly.</summary>
public static class SkiaRenderRegistrationExtensions
{
    /// <summary>Configures the native Skia drawing services.</summary>
    /// <param name="registration">The managed services to configure.</param>
    extension(PdfRenderBackendRegistration registration)
    {
        /// <summary>Adds Skia services to an immutable drawing registration.</summary>
        /// <returns>The registration using Skia services.</returns>
        public PdfRenderBackendRegistration UseSkia()
        {
            ArgumentNullException.ThrowIfNull(registration);
            return registration with
            {
                Backend = new SkiaRenderBackend(),
                Fonts = new SkiaFontProvider(),
                Images = new SkiaPdfImageCodec(),
            };
        }
    }
}
