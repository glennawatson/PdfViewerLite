// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Drawing;

namespace HyperPdfLibrary.Content;

/// <summary>A device that records everything drawn on it into a picture.</summary>
public interface IPictureDevice : IContentDevice, IDisposable
{
    /// <summary>Gets the distinct images held by this recording.</summary>
    HyperPdfLibrary.Rendering.PictureWeight Weight { get; }

    /// <summary>Ends recording.</summary>
    /// <returns>The owned picture, which retains everything needed for replay after the recorder and source images are disposed.</returns>
    IPdfRenderPicture Finish();
}
