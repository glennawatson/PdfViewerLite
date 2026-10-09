// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>One compiled PostScript calculator instruction.</summary>
/// <param name="Code">The operation.</param>
/// <param name="Operand">The value pushed by <see cref="PostScriptOpCode.Push"/>.</param>
/// <param name="Target">The instruction a jump goes to.</param>
internal readonly record struct PostScriptInstruction(PostScriptOpCode Code, PostScriptValue Operand, int Target);
