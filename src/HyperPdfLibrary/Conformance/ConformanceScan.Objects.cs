// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <content>The object-by-object part of the scan: filters, scripts, transparency and external content.</content>
internal sealed partial class ConformanceScan
{
    /// <summary>Determines whether a value is a number below 1.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBelowOne(PdfValue value) => value.IsNumber && value.AsNumber() < 1;

    /// <summary>Determines whether a <c>/BM</c> value names a blend mode other than Normal; an array is read by its first name.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the mode changes how the page composites.</returns>
    private static bool IsBlended(PdfValue value)
    {
        if (value.AsArray() is { } modes)
        {
            value = modes.Get(0);
        }

        return value.AsName() is { IsNone: false } name && name.ToKnownName() is not (KnownName.Normal or KnownName.Compatible);
    }

    /// <summary>Determines whether a <c>/Filter</c> value includes LZW.</summary>
    /// <param name="filter">The value: a name or an array of names.</param>
    /// <returns><see langword="true"/> when LZW is in the chain.</returns>
    private static bool UsesLzw(PdfValue filter)
    {
        if (filter.AsArray() is not { } chain)
        {
            return IsLzw(filter);
        }

        for (var i = 0; i < chain.Count; i++)
        {
            if (IsLzw(chain.Get(i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a value is the LZW filter name.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsLzw(PdfValue value) => value.IsName(KnownName.LZWDecode) || value.IsName(KnownName.LZW);

    /// <summary>Reads every object once and counts what its dictionaries hold.</summary>
    private void ScanObjects()
    {
        var objects = _document.Objects;
        for (var number = 1; number < objects.Size; number++)
        {
            PdfOpenContext.ThrowIfCancelled(objects.Context);
            var value = StoreReading.GetObject(objects, new(number, 0));
            if (value.AsStream() is { } stream)
            {
                CheckStream(stream.Dictionary);
            }

            ScanValue(value, 0);
        }
    }

    /// <summary>Counts the stream-only features of a stream dictionary: LZW and external content.</summary>
    /// <param name="dictionary">The stream's dictionary.</param>
    private void CheckStream(PdfDictionary dictionary)
    {
        if (UsesLzw(dictionary.Get(KnownName.Filter)))
        {
            _lzw++;
        }

        if (dictionary.ContainsKey(KnownName.F) || dictionary.ContainsKey(KnownName.Ref) || dictionary.ContainsKey(KnownName.OPI))
        {
            _external++;
        }
    }

    /// <summary>Walks the direct dictionaries and arrays inside a value. References are not followed; each object is scanned on its own.</summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    private void ScanValue(PdfValue value, int depth)
    {
        if (depth >= PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            CheckTransparency(dictionary);
            CheckScript(dictionary);
            for (var i = 0; i < dictionary.Count; i++)
            {
                ScanValue(dictionary.GetValueAt(i), depth + 1);
            }
        }
        else if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                ScanValue(array.GetRaw(i), depth + 1);
            }
        }
    }

    /// <summary>Counts JavaScript actions.</summary>
    /// <param name="dictionary">A dictionary.</param>
    private void CheckScript(PdfDictionary dictionary)
    {
        if (dictionary.IsName(KnownName.S, KnownName.JavaScript))
        {
            _scripts++;
        }
    }

    /// <summary>Counts the transparency features a dictionary holds.</summary>
    /// <param name="dictionary">A dictionary.</param>
    private void CheckTransparency(PdfDictionary dictionary)
    {
        if (dictionary.GetRaw(KnownName.SMask) is { IsNull: false } mask && !mask.IsName(KnownName.NoneName))
        {
            _softMasks++;
        }

        if (IsBelowOne(dictionary.Get(KnownName.CA)) || IsBelowOne(dictionary.Get(_lowerCa)))
        {
            _alphas++;
        }

        if (IsBlended(dictionary.Get(KnownName.BM)))
        {
            _blendModes++;
        }

        if (dictionary.GetDictionary(KnownName.Group) is { } group && group.IsName(KnownName.S, KnownName.Transparency))
        {
            _groups++;
        }
    }
}
