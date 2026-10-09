// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>One operand of a PostScript calculator program: an integer, a real or a boolean.</summary>
/// <param name="Number">The value; booleans are 1 or 0.</param>
/// <param name="Kind">The operand type.</param>
internal readonly record struct PostScriptValue(double Number, PostScriptValueKind Kind)
{
    /// <summary>Gets a value indicating whether this is a boolean.</summary>
    internal bool IsBoolean => Kind == PostScriptValueKind.Boolean;

    /// <summary>Gets a value indicating whether this is an integer.</summary>
    internal bool IsInteger => Kind == PostScriptValueKind.Integer;

    /// <summary>Creates a real.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operand.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PostScriptValue Real(double value) => new(value, PostScriptValueKind.Real);

    /// <summary>Creates an integer.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operand.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PostScriptValue Integer(double value) => new(value, PostScriptValueKind.Integer);

    /// <summary>Creates a boolean.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The operand.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static PostScriptValue Boolean(bool value) => new(value ? 1 : 0, PostScriptValueKind.Boolean);
}
