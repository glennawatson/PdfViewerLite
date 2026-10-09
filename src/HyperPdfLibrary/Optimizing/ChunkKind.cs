// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Optimizing;

/// <summary>The part of a file one chunk of <see cref="OptimizedFileWriter"/> output holds.</summary>
internal enum ChunkKind
{
    /// <summary>The header and binary comment.</summary>
    Header = 0,

    /// <summary>One object written plainly.</summary>
    Plain = 1,

    /// <summary>One object stream of packed objects.</summary>
    ObjectStream = 2,

    /// <summary>The cross-reference section and trailer.</summary>
    Xref = 3,
}
