// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Drawing;

/// <summary>An owned recording supplied by a drawing backend.</summary>
public interface IPdfRenderPicture : IDisposable
{
    /// <summary>Gets the recording storage in bytes, excluding shared images.</summary>
    long ApproximateBytesUsed { get; }
}
