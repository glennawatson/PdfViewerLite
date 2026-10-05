// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.Controls;

/// <summary>The inclusive range of tiles of one page that cover an area.</summary>
/// <param name="FirstColumn">The first column.</param>
/// <param name="LastColumn">The last column.</param>
/// <param name="FirstRow">The first row.</param>
/// <param name="LastRow">The last row.</param>
internal readonly record struct TileWindow(int FirstColumn, int LastColumn, int FirstRow, int LastRow);
