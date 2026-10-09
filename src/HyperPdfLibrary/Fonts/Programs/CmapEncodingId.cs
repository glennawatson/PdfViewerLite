// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Programs;

/// <summary>The platform and encoding ids that identify a 'cmap' subtable.</summary>
/// <param name="Platform">The platform id.</param>
/// <param name="Encoding">The encoding id.</param>
[DebuggerDisplay("Platform {Platform}, encoding {Encoding}")]
internal readonly record struct CmapEncodingId(int Platform, int Encoding);
