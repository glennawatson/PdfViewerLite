// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>Runs JavaScript. The library reads scripts; running them is left to the host.</summary>
/// <param name="Script">The script.</param>
[DebuggerDisplay("JavaScriptAction: {Script.Length} characters")]
public sealed record JavaScriptAction(string Script);
