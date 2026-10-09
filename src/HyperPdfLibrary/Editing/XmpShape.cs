// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Editing;

/// <summary>How an XMP property's value is written.</summary>
internal enum XmpShape
{
    /// <summary>Plain text.</summary>
    Simple = 0,

    /// <summary>A language alternative with one x-default entry.</summary>
    Alternative = 1,

    /// <summary>An ordered array with one entry.</summary>
    Sequence = 2,
}
