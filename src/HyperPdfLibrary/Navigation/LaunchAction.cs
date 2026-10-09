// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Opens another file with its application.</summary>
/// <param name="File">The file, as written in the document.</param>
[DebuggerDisplay("LaunchAction: {File}")]
public sealed record LaunchAction(string File);
