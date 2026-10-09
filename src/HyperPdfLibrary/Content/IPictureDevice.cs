// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using SkiaSharp;

namespace HyperPdfLibrary.Content;

/// <summary>A device that records everything drawn on it into a picture.</summary>
internal interface IPictureDevice : IContentDevice, IDisposable
{
    /// <summary>Ends recording.</summary>
    /// <returns>The picture; the caller owns it.</returns>
    SKPicture Finish();
}
