// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>Runs one PostScript calculator operator on the stack.</summary>
/// <param name="stack">The operand stack.</param>
internal delegate void PostScriptOperator(ref PostScriptStack stack);
