// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jbig2;

/// <summary>The outcome of processing a segment or a stream of segments.</summary>
internal enum Jbig2Status
{
    /// <summary>Processing went well and may continue.</summary>
    Success = 0,

    /// <summary>An end-of-page or end-of-file segment stopped processing.</summary>
    EndReached = 1,

    /// <summary>The data is damaged; processing stopped.</summary>
    Failure = 2,
}
