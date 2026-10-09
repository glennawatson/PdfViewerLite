// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Navigation;

/// <summary>A named viewer action, such as NextPage.</summary>
/// <param name="Name">The action's name.</param>
[DebuggerDisplay("NamedAction: {Name}")]
public sealed record NamedAction(string Name);
