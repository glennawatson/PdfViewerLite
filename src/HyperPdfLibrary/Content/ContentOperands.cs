// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Operands operations over its owned state.</summary>
internal static class ContentOperands
{
    /// <summary>The numbers in a matrix.</summary>
    internal const int MatrixNumbers = 6;

    /// <summary>Reads a matrix entry.</summary>
    /// <param name = "dictionary">The dictionary.</param>
    /// <param name = "key">The key.</param>
    /// <returns>The matrix; identity when missing or short.</returns>
    internal static Matrix3x2 ReadMatrix(PdfDictionary dictionary, KnownName key)
    {
        Span<float> numbers = stackalloc float[ContentOperands.MatrixNumbers];
        return dictionary.GetArray(key) is { Count: >= ContentOperands.MatrixNumbers } array
        && array.ReadNumbers(numbers) >= ContentOperands.MatrixNumbers ? new(
            numbers[0],
            numbers[1],
            numbers[2],
            numbers[ContentGraphicsState.MatrixD],
            numbers[ContentGraphicsState.MatrixE],
            numbers[ContentGraphicsState.MatrixF]) : Matrix3x2.Identity;
    }

    /// <summary>Reads the numbers in the body of an array.</summary>
    /// <param name = "body">The bytes between the brackets.</param>
    /// <param name = "destination">Receives the numbers.</param>
    /// <returns>The numbers read; extra numbers are ignored.</returns>
    internal static int ReadNumbers(ReadOnlySpan<byte> body, Span<float> destination)
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
