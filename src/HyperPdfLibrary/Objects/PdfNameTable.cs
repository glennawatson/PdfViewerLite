// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Collections.Frozen;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Text;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// Interns the names of one document. Known names resolve through a shared frozen table; names the library does not know
/// are added to this table on first sight. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfNameTable: {Count} names")]
public sealed class PdfNameTable
{
    /// <summary>The largest UTF-8 encoding of a name given as text: four bytes per character.</summary>
    private const int MaxEncodedName = PdfLimits.MaxNameLength * 4;

    /// <summary>The most spellings cached by text; callers pass library constants, but the cap bounds a caller that does not.</summary>
    private const int MaxTextCache = 4096;

    /// <summary>The shared table of known names.</summary>
    private static readonly FrozenDictionary<byte[], int> KnownIds = CreateKnownIds();

    /// <summary>Looks up known names by their bytes without allocating.</summary>
    private static readonly FrozenDictionary<byte[], int>.AlternateLookup<ReadOnlySpan<byte>> KnownLookup =
        KnownIds.GetAlternateLookup<ReadOnlySpan<byte>>();

    /// <summary>The UTF-8 spelling of each known name.</summary>
    private static readonly byte[][] KnownSpellings = CreateKnownSpellings();

    /// <summary>The names already interned from text, so a repeated lookup skips the encoding and the lock.</summary>
    private readonly ConcurrentDictionary<string, PdfName> _byText = new(StringComparer.Ordinal);

    /// <summary>Guards <see cref="_ids"/> and <see cref="_spellings"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The ids of the names this document added.</summary>
    private readonly Dictionary<byte[], int> _ids = [with(ByteArrayComparer.Instance)];

    /// <summary>The spellings of the names this document added, indexed by id minus the known count.</summary>
    private readonly List<byte[]> _spellings = [];

    /// <summary>Gets the number of names, known and added.</summary>
    public int Count
    {
        get
        {
            lock (_gate)
            {
                return KnownNameSpellings.Count + _spellings.Count;
            }
        }
    }

    /// <summary>Gets the spelling of a known name.</summary>
    /// <param name="name">The known name.</param>
    /// <returns>The UTF-8 bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ReadOnlySpan<byte> GetKnownSpelling(KnownName name) => KnownSpellings[(int)name];

    /// <summary>Finds a known name by its bytes.</summary>
    /// <param name="spelling">The name's bytes, without the slash and with #xx escapes decoded.</param>
    /// <param name="name">The known name.</param>
    /// <returns><see langword="true"/> when the name is known.</returns>
    public static bool TryGetKnown(ReadOnlySpan<byte> spelling, out PdfName name)
    {
        if (KnownLookup.TryGetValue(spelling, out var id))
        {
            name = new(id);
            return true;
        }

        name = default;
        return false;
    }

    /// <summary>Interns a name.</summary>
    /// <param name="spelling">The name's bytes, without the slash and with #xx escapes decoded.</param>
    /// <returns>The name.</returns>
    public PdfName Intern(ReadOnlySpan<byte> spelling)
    {
        if (KnownLookup.TryGetValue(spelling, out var known))
        {
            return new(known);
        }

        lock (_gate)
        {
            var lookup = _ids.GetAlternateLookup<ReadOnlySpan<byte>>();
            if (lookup.TryGetValue(spelling, out var id))
            {
                return new(id);
            }

            var bytes = spelling.ToArray();
            id = KnownNameSpellings.Count + _spellings.Count;
            _spellings.Add(bytes);
            _ids.Add(bytes, id);
            return new(id);
        }
    }

    /// <summary>Interns a name given as text.</summary>
    /// <param name="spelling">The name, without the slash.</param>
    /// <returns>The name.</returns>
    public PdfName Intern(string spelling)
    {
        ArgumentNullException.ThrowIfNull(spelling);
        if (_byText.TryGetValue(spelling, out var cached))
        {
            return cached;
        }

        var name = InternEncoded(spelling);
        if (_byText.Count < MaxTextCache)
        {
            _ = _byText.TryAdd(spelling, name);
        }

        return name;
    }

    /// <summary>Gets the spelling of a name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The UTF-8 bytes, or empty when the name is not from this table.</returns>
    public ReadOnlySpan<byte> GetSpelling(PdfName name)
    {
        if (name.IsKnown)
        {
            return KnownSpellings[name.Id];
        }

        lock (_gate)
        {
            var index = name.Id - KnownNameSpellings.Count;
            return (uint)index < (uint)_spellings.Count ? _spellings[index] : [];
        }
    }

    /// <summary>Gets the spelling of a name as text.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The name decoded as UTF-8.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetString(PdfName name) => Encoding.UTF8.GetString(GetSpelling(name));

    /// <summary>Determines whether a name is spelled a certain way.</summary>
    /// <param name="name">The name.</param>
    /// <param name="spelling">The expected bytes.</param>
    /// <returns><see langword="true"/> when they match.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool NameEquals(PdfName name, ReadOnlySpan<byte> spelling) => GetSpelling(name).SequenceEqual(spelling);

    /// <summary>Builds the shared table of known names.</summary>
    /// <returns>The table.</returns>
    private static FrozenDictionary<byte[], int> CreateKnownIds()
    {
        var ids = new Dictionary<byte[], int>(KnownNameSpellings.Count, ByteArrayComparer.Instance);
        for (var i = 1; i < KnownNameSpellings.Count; i++)
        {
            ids[Encoding.UTF8.GetBytes(KnownNameSpellings.All[i])] = i;
        }

        return ids.ToFrozenDictionary(ByteArrayComparer.Instance);
    }

    /// <summary>Builds the UTF-8 spellings of the known names.</summary>
    /// <returns>The spellings, indexed by id.</returns>
    private static byte[][] CreateKnownSpellings()
    {
        var spellings = new byte[KnownNameSpellings.Count][];
        for (var i = 0; i < spellings.Length; i++)
        {
            spellings[i] = Encoding.UTF8.GetBytes(KnownNameSpellings.All[i]);
        }

        return spellings;
    }

    /// <summary>Encodes a spelling as UTF-8 and interns it.</summary>
    /// <param name="spelling">The name, without the slash.</param>
    /// <returns>The name.</returns>
    private PdfName InternEncoded(string spelling)
    {
        Span<byte> buffer = stackalloc byte[MaxEncodedName];
        var length = Encoding.UTF8.GetBytes(spelling.AsSpan(0, Math.Min(spelling.Length, PdfLimits.MaxNameLength)), buffer);
        return Intern(buffer[..Math.Min(length, PdfLimits.MaxNameLength)]);
    }
}
