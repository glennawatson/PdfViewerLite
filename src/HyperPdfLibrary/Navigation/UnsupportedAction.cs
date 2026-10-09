// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>An action the library does not interpret, or whose target could not be resolved. It is not corruption: the <c>/S</c> name says what it was.</summary>
/// <param name="Subtype">The <c>/S</c> name, or an empty string when the action has none.</param>
[DebuggerDisplay("UnsupportedAction: {Subtype}")]
public sealed record UnsupportedAction(string Subtype);
