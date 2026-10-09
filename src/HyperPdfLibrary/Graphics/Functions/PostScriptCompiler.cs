// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>
/// Compiles a PostScript calculator program once into a flat instruction array. Procedures for <c>if</c> and
/// <c>ifelse</c> become forward jumps, so a program runs in one pass with no recursion.
/// </summary>
internal static class PostScriptCompiler
{
    /// <summary>The deepest nesting of procedures compiled.</summary>
    private const int MaxNesting = 64;

    /// <summary>The most instructions compiled.</summary>
    private const int MaxInstructions = 1 << 16;

    /// <summary>Compiles a program.</summary>
    /// <param name="program">The program text, starting with <c>{</c>.</param>
    /// <returns>The instructions, or <see langword="null"/> when the program is invalid.</returns>
    internal static PostScriptInstruction[]? Compile(ReadOnlySpan<byte> program)
    {
        var reader = new PostScriptReader(program);
        if (reader.Next(out _) != PostScriptTokenKind.Open)
        {
            return null;
        }

        var code = new List<PostScriptInstruction>();
        return CompileProcedure(ref reader, code, 0) ? [.. code] : null;
    }

    /// <summary>Compiles the body of a procedure up to and including its closing brace.</summary>
    /// <param name="reader">The token reader, after the opening brace.</param>
    /// <param name="code">The instructions.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when the procedure compiled.</returns>
    private static bool CompileProcedure(ref PostScriptReader reader, List<PostScriptInstruction> code, int depth)
    {
        while (code.Count < MaxInstructions)
        {
            var kind = reader.Next(out var text);
            switch (kind)
            {
                case PostScriptTokenKind.Close:
                {
                    return true;
                }

                case PostScriptTokenKind.End:
                {
                    // Tolerate a missing closing brace at the end of the program.
                    return depth == 0;
                }

                case PostScriptTokenKind.Open:
                {
                    if (depth >= MaxNesting || !CompileConditional(ref reader, code, depth + 1))
                    {
                        return false;
                    }

                    break;
                }

                default:
                {
                    if (!CompileWord(text, code))
                    {
                        return false;
                    }

                    break;
                }
            }
        }

        return false;
    }

    /// <summary>Compiles a number or an operator.</summary>
    /// <param name="text">The token text.</param>
    /// <param name="code">The instructions.</param>
    /// <returns><see langword="true"/> when the token is valid.</returns>
    private static bool CompileWord(ReadOnlySpan<byte> text, List<PostScriptInstruction> code)
    {
        if (PostScriptOperators.TryGetOpCode(text, out var opCode))
        {
            code.Add(new(opCode, default, 0));
            return true;
        }

        if (!double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var number))
        {
            return false;
        }

        var isInteger = text.IndexOfAny((byte)'.', (byte)'e', (byte)'E') < 0 && number >= int.MinValue && number <= int.MaxValue;
        code.Add(new(PostScriptOpCode.Push, isInteger ? PostScriptValue.Integer(number) : PostScriptValue.Real(number), 0));
        return true;
    }

    /// <summary>Compiles <c>{ proc } if</c> or <c>{ proc1 } { proc2 } ifelse</c>, after the first opening brace.</summary>
    /// <param name="reader">The token reader.</param>
    /// <param name="code">The instructions.</param>
    /// <param name="depth">The nesting depth of the procedures.</param>
    /// <returns><see langword="true"/> when the conditional compiled.</returns>
    private static bool CompileConditional(ref PostScriptReader reader, List<PostScriptInstruction> code, int depth)
    {
        var skipFirst = code.Count;
        code.Add(new(PostScriptOpCode.JumpIfFalse, default, 0));
        if (!CompileProcedure(ref reader, code, depth))
        {
            return false;
        }

        var skipSecond = code.Count;
        code.Add(new(PostScriptOpCode.Jump, default, 0));
        var kind = reader.Next(out var text);
        if (kind == PostScriptTokenKind.Word && text.SequenceEqual("if"u8))
        {
            code.RemoveAt(skipSecond);
            code[skipFirst] = code[skipFirst] with { Target = code.Count };
            return true;
        }

        if (kind != PostScriptTokenKind.Open || !CompileProcedure(ref reader, code, depth))
        {
            return false;
        }

        if (reader.Next(out text) != PostScriptTokenKind.Word || !text.SequenceEqual("ifelse"u8))
        {
            return false;
        }

        code[skipFirst] = code[skipFirst] with { Target = skipSecond + 1 };
        code[skipSecond] = code[skipSecond] with { Target = code.Count };
        return true;
    }
}
