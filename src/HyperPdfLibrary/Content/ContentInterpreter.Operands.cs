// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <content>Helpers that read operand bodies.</content>
internal sealed partial class ContentInterpreter
{
    /// <summary>The numbers in a matrix.</summary>
    private const int MatrixNumbers = 6;

    /// <summary>Reads a matrix entry.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <returns>The matrix; identity when missing or short.</returns>
    private static Matrix3x2 ReadMatrix(PdfDictionary dictionary, KnownName key)
    {
        Span<float> numbers = stackalloc float[MatrixNumbers];
        return dictionary.GetArray(key) is { Count: >= MatrixNumbers } array && array.ReadNumbers(numbers) >= MatrixNumbers
            ? new(numbers[0], numbers[1], numbers[2], numbers[MatrixD], numbers[MatrixE], numbers[MatrixF])
            : Matrix3x2.Identity;
    }

    /// <summary>Reads the numbers in the body of an array.</summary>
    /// <param name="body">The bytes between the brackets.</param>
    /// <param name="destination">Receives the numbers.</param>
    /// <returns>The numbers read; extra numbers are ignored.</returns>
    private static int ReadNumbers(ReadOnlySpan<byte> body, Span<float> destination)
    {
        var lexer = new PdfLexer(body);
        var count = 0;
        while (count < destination.Length)
        {
            var kind = lexer.Next();
            if (kind == PdfTokenKind.EndOfData)
            {
                break;
            }

            if (kind != PdfTokenKind.Number || !PdfNumber.TryParseSingle(lexer.Lexeme, out var number))
            {
                continue;
            }

            destination[count] = number;
            count++;
        }

        return count;
    }
}
