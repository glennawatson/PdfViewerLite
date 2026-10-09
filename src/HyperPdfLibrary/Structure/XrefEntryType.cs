// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Structure;

/// <summary>The type of a cross-reference entry.</summary>
internal enum XrefEntryType
{
    /// <summary>A free or missing object.</summary>
    Free = 0,

    /// <summary>An object at a file offset.</summary>
    InFile = 1,

    /// <summary>An object inside an object stream.</summary>
    Compressed = 2,
}
