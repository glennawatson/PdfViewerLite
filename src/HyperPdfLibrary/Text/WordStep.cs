// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>What a text search does after matching one word of the query.</summary>
internal enum WordStep
{
    /// <summary>Go on to the next word.</summary>
    Next = 0,

    /// <summary>The whole query matched.</summary>
    Done = 1,

    /// <summary>Start again from the first word.</summary>
    Restart = 2,

    /// <summary>No more matches.</summary>
    Fail = 3,
}
