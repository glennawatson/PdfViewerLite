// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>The direction lines of text flow in.</summary>
internal enum TextOrientation
{
    /// <summary>Not known.</summary>
    Unknown = 0,

    /// <summary>Lines run across the page.</summary>
    Horizontal = 1,

    /// <summary>Lines run down the page.</summary>
    Vertical = 2,
}
