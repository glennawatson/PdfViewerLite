// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Filters;

/// <summary>How applying one byte filter went.</summary>
internal enum FilterResult
{
    /// <summary>The filter ran and its data ended cleanly.</summary>
    Applied = 0,

    /// <summary>The filter is not one this library knows; the data passed through.</summary>
    Unknown = 1,

    /// <summary>The data was truncated or damaged; the part before the fault was kept.</summary>
    Damaged = 2,
}
