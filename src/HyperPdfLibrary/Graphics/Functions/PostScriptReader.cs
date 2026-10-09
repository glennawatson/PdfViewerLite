// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Graphics.Functions;

/// <summary>Splits a PostScript calculator program into tokens.</summary>
/// <param name="program">The program text.</param>
internal ref struct PostScriptReader(ReadOnlySpan<byte> program)
{
    /// <summary>The program text.</summary>
    private readonly ReadOnlySpan<byte> _program = program;

    /// <summary>The read position.</summary>
    private int _position;

    /// <summary>Reads the next token, skipping white space and comments.</summary>
    /// <param name="text">The token text for words.</param>
    /// <returns>The token kind.</returns>
    internal PostScriptTokenKind Next(out ReadOnlySpan<byte> text)
    {
        SkipSpace();
        text = default;
        if (_position >= _program.Length)
        {
            return PostScriptTokenKind.End;
        }

        var current = _program[_position];
        if (current is (byte)'{' or (byte)'}')
        {
            _position++;
            return current == (byte)'{' ? PostScriptTokenKind.Open : PostScriptTokenKind.Close;
        }

        var rest = _program[_position..];
        var length = rest.IndexOfAny(PdfCharacters.TokenEnd);
        length = length switch
        {
            < 0 => rest.Length,

            // A stray delimiter such as '(' is not part of the calculator language; it becomes an invalid word.
            0 => 1,
            _ => length,
        };

        text = rest[..length];
        _position += length;
        return PostScriptTokenKind.Word;
    }

    /// <summary>Skips white space and comments.</summary>
    private void SkipSpace()
    {
        while (_position < _program.Length)
        {
            var current = _program[_position];
            if (current == (byte)'%')
            {
                var lineEnd = _program[_position..].IndexOfAny(PdfCharacters.LineEnd);
                _position = lineEnd < 0 ? _program.Length : _position + lineEnd;
            }
            else if (PdfCharacters.IsWhitespace(current))
            {
                _position++;
            }
            else
            {
                return;
            }
        }
    }
}
