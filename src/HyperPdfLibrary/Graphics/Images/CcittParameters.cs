// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>The CCITTFaxDecode filter parameters (PDF 32000-1 table 11).</summary>
/// <param name="K">The coding scheme: negative for Group 4, zero for Group 3 one-dimensional, positive for mixed Group 3.</param>
/// <param name="Columns">The width of the encoded image in pixels.</param>
/// <param name="Rows">The height of the encoded image in pixels, or zero when not given.</param>
/// <param name="BlackIs1">Whether a 1 bit means black in the decoded output.</param>
/// <param name="EncodedByteAlign">Whether each encoded line starts on a byte boundary.</param>
/// <param name="EndOfLine">Whether each encoded line is preceded by an end-of-line code.</param>
/// <param name="EndOfBlock">Whether the data ends with an end-of-block code.</param>
internal readonly record struct CcittParameters(
    int K,
    int Columns,
    int Rows,
    bool BlackIs1,
    bool EncodedByteAlign,
    bool EndOfLine,
    bool EndOfBlock)
{
    /// <summary>The default width in pixels.</summary>
    private const int DefaultColumns = 1728;

    /// <summary>Reads the parameters from a /DecodeParms dictionary, using the specification defaults for missing entries.</summary>
    /// <param name="parms">The parameters dictionary, or <see langword="null"/>.</param>
    /// <returns>The parameters.</returns>
    internal static CcittParameters FromDictionary(PdfDictionary? parms) =>
        parms is null
            ? new(0, DefaultColumns, 0, false, false, false, true)
            : new(
                parms.GetInt32(KnownName.K, 0),
                parms.GetInt32(KnownName.Columns, DefaultColumns),
                parms.GetInt32(KnownName.Rows, 0),
                parms.GetBoolean(KnownName.BlackIs1, false),
                parms.GetBoolean(KnownName.EncodedByteAlign, false),
                parms.GetBoolean(KnownName.EndOfLine, false),
                parms.GetBoolean(KnownName.EndOfBlock, true));
}
