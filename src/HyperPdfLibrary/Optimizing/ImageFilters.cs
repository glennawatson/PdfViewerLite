// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Reads a stream's filter chain.</summary>
internal static class ImageFilters
{
    /// <summary>Gets the number of filters in a /Filter value.</summary>
    /// <param name="filters">The /Filter value: a name, an array or null.</param>
    /// <returns>The count.</returns>
    internal static int Count(PdfValue filters) => filters.Kind switch
    {
        PdfKind.Name => 1,
        PdfKind.Array => filters.AsArray()!.Count,
        _ => 0,
    };

    /// <summary>Gets one filter of a /Filter value.</summary>
    /// <param name="filters">The /Filter value.</param>
    /// <param name="index">The filter's index.</param>
    /// <returns>The filter, or <see cref="KnownName.None"/> for an unknown one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static KnownName At(PdfValue filters, int index) =>
        (filters.Kind == PdfKind.Array ? filters.AsArray()!.GetName(index) : filters.AsName()).ToKnownName();

    /// <summary>Finds the image codec in a filter chain.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The codec.</returns>
    internal static CodecKind Codec(PdfDictionary dictionary)
    {
        var filters = dictionary.Get(KnownName.Filter);
        var count = Count(filters);
        var kind = CodecKind.Bytes;
        for (var i = 0; i < count; i++)
        {
            kind = At(filters, i) switch
            {
                KnownName.Crypt => CodecKind.Crypt,
                KnownName.DCTDecode or KnownName.DCT => CodecKind.Jpeg,
                KnownName.JPXDecode => CodecKind.Jpeg2000,
                KnownName.JBIG2Decode => CodecKind.Jbig2,
                KnownName.CCITTFaxDecode or KnownName.CCF => CodecKind.Fax,
                _ => kind,
            };
        }

        return kind;
    }

    /// <summary>Gets the last filter's parameters, which an image codec reads.</summary>
    /// <param name="dictionary">The image dictionary.</param>
    /// <returns>The parameters, or <see langword="null"/>.</returns>
    internal static PdfDictionary? CodecParameters(PdfDictionary dictionary)
    {
        var parameters = dictionary.Get(KnownName.DecodeParms);
        return parameters.AsArray() is { Count: > 0 } array ? array.GetDictionary(array.Count - 1) : parameters.AsDictionary();
    }
}
