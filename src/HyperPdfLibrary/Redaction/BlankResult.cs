// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Redaction;

/// <summary>What blanking an image's pixels did.</summary>
internal enum BlankResult
{
    /// <summary>No pixel lay under an area.</summary>
    Unchanged = 0,

    /// <summary>Pixels were blanked and the image was replaced.</summary>
    Blanked = 1,

    /// <summary>The image could not be blanked pixel by pixel, so it is removed whole.</summary>
    Removed = 2,
}
