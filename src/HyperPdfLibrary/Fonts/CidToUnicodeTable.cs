// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Fonts;

/// <summary>
/// The Unicode value of each CID of an Adobe character collection, taken from Adobe's Adobe-*-UCS2 ToUnicode maps,
/// filled from the Unicode columns of cid2code.txt. A flat array indexed by CID, so a lookup is one bounds check; each collection is read from the
/// assembly once, on first use.
/// </summary>
[DebuggerDisplay("CidToUnicodeTable: {_values.Length} CIDs")]
internal sealed class CidToUnicodeTable
{
    /// <summary>The format version of the packed resource.</summary>
    private const byte Version = 1;

    /// <summary>The number of collections, including <see cref="CjkScript.None"/>.</summary>
    private const int CollectionCount = (int)CjkScript.Korean + 1;

    /// <summary>The largest CID a table can hold.</summary>
    private const int MaxCids = ushort.MaxValue + 1;

    /// <summary>The tables read so far, by collection.</summary>
    private static readonly CidToUnicodeTable?[] Tables = new CidToUnicodeTable?[CollectionCount];

    /// <summary>Guards reading the tables.</summary>
    private static readonly Lock Gate = new();

    /// <summary>The Unicode value of each CID; zero when none.</summary>
    private readonly char[] _values;

    /// <summary>Initializes a new instance of the <see cref="CidToUnicodeTable"/> class.</summary>
    /// <param name="values">The Unicode value of each CID.</param>
    private CidToUnicodeTable(char[] values) => _values = values;

    /// <summary>Gets the table of a collection.</summary>
    /// <param name="collection">The collection.</param>
    /// <returns>The table, or <see langword="null"/> for <see cref="CjkScript.None"/>.</returns>
    internal static CidToUnicodeTable? Get(CjkScript collection)
    {
        var index = (int)collection;
        if (collection == CjkScript.None || (uint)index >= CollectionCount)
        {
            return null;
        }

        if (Volatile.Read(ref Tables[index]) is { } table)
        {
            return table;
        }

        lock (Gate)
        {
            table = Tables[index] ?? Read(collection);
            Volatile.Write(ref Tables[index], table);
            return table;
        }
    }

    /// <summary>Reads a collection's table from the assembly without caching it.</summary>
    /// <param name="collection">The collection.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The resource is missing or damaged.</exception>
    internal static CidToUnicodeTable Read(CjkScript collection)
    {
        var name = collection switch
        {
            CjkScript.Japanese => "Adobe-Japan1-UCS2",
            CjkScript.SimplifiedChinese => "Adobe-GB1-UCS2",
            CjkScript.TraditionalChinese => "Adobe-CNS1-UCS2",
            _ => "Adobe-Korea1-UCS2",
        };
        var data = new PooledBuffer(0);
        try
        {
            if (!CMapResources.TryRead(name, ref data))
            {
                throw new InvalidDataException($"The CID-to-Unicode table {name} is missing.");
            }

            return Parse(data.WrittenSpan);
        }
        finally
        {
            data.Dispose();
        }
    }

    /// <summary>Gets the Unicode value of a CID.</summary>
    /// <param name="cid">The CID.</param>
    /// <returns>The UTF-16 value, or zero when the collection has none.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int Lookup(int cid) => (uint)cid < (uint)_values.Length ? _values[cid] : 0;

    /// <summary>Parses a packed table: a version, the CID count, then each value as a zigzag delta from the previous one.</summary>
    /// <param name="data">The decompressed resource.</param>
    /// <returns>The table.</returns>
    /// <exception cref="InvalidDataException">The data is damaged.</exception>
    private static CidToUnicodeTable Parse(ReadOnlySpan<byte> data)
    {
        var reader = new PackedDataReader(data);
        var count = reader.ReadByte() == Version ? reader.ReadNumber() : uint.MaxValue;
        if (count > MaxCids)
        {
            throw new InvalidDataException("The CID-to-Unicode table is damaged.");
        }

        var values = new char[count];
        var previous = 0;
        for (var cid = 0; cid < values.Length; cid++)
        {
            previous += reader.ReadSigned();
            values[cid] = (char)previous;
        }

        return new(values);
    }
}
