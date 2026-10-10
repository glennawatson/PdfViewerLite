// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Extensions;

/// <summary>Finds dictionary keys that ISO 32000-2 does not define and checks whether their values read cleanly. It never throws for unknown keys.</summary>
internal sealed class UnknownEntryScanner
{
    /// <summary>The deepest value nesting checked.</summary>
    private const int MaxCheckDepth = 8;

    /// <summary>The most values checked inside one entry.</summary>
    private const int MaxCheckValues = 512;

    /// <summary>The longest developer prefix.</summary>
    private const int MaxPrefixLength = 8;

    /// <summary>The object store.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The ids of the known catalog keys.</summary>
    private readonly HashSet<int> _catalogKeys;

    /// <summary>The ids of the known page keys.</summary>
    private readonly HashSet<int> _pageKeys;

    /// <summary>The developer prefixes the catalog declares in <c>/Extensions</c>.</summary>
    private readonly HashSet<string> _declared = [with(StringComparer.Ordinal)];

    /// <summary>Initializes a new instance of the <see cref="UnknownEntryScanner"/> class.</summary>
    /// <param name="store">The object store.</param>
    internal UnknownEntryScanner(PdfObjectStore store)
    {
        _store = store;
        _catalogKeys = Intern(store, PdfKnownKeys.Catalog);
        _pageKeys = Intern(store, PdfKnownKeys.Page);
        var extensions = store.Catalog.GetDictionary(KnownName.Extensions);
        for (var i = 0; extensions is not null && i < extensions.Count; i++)
        {
            _ = _declared.Add(store.Names.GetString(extensions.GetKeyAt(i)));
        }
    }

    /// <summary>Gets the developer prefix of a second-class name such as <c>ADBE_Thing</c>.</summary>
    /// <param name="key">The key.</param>
    /// <returns>The prefix, or null when the key has none.</returns>
    internal static string? DeveloperPrefix(string key)
    {
        var underscore = key.IndexOf('_', StringComparison.Ordinal);
        if (underscore is < 1 or > MaxPrefixLength)
        {
            return null;
        }

        foreach (var c in key.AsSpan(0, underscore))
        {
            if (!char.IsAsciiLetterUpper(c) && !char.IsAsciiDigit(c))
            {
                return null;
            }
        }

        return key[..underscore];
    }

    /// <summary>Scans a dictionary and adds its unknown entries.</summary>
    /// <param name="dictionary">The catalog or page dictionary.</param>
    /// <param name="owner">The kind of dictionary.</param>
    /// <param name="pageIndex">The page, or null for the catalog.</param>
    /// <param name="output">The result list.</param>
    internal void Scan(PdfDictionary dictionary, PdfEntryOwner owner, int? pageIndex, List<PdfUnknownEntry> output)
    {
        var known = owner == PdfEntryOwner.Catalog ? _catalogKeys : _pageKeys;
        for (var i = 0; i < dictionary.Count; i++)
        {
            var key = dictionary.GetKeyAt(i);
            if (known.Contains(key.Id))
            {
                continue;
            }

            var text = _store.Names.GetString(key);
            var prefix = DeveloperPrefix(text);
            var raw = dictionary.GetValueAt(i);
            output.Add(new(owner, pageIndex, text, KindOf(raw), ReadsCleanly(raw), prefix is not null, prefix is not null && _declared.Contains(prefix)));
        }
    }

    /// <summary>Interns the known key spellings and collects their ids.</summary>
    /// <param name="store">The object store.</param>
    /// <param name="keys">The key spellings.</param>
    /// <returns>The ids.</returns>
    private static HashSet<int> Intern(PdfObjectStore store, IReadOnlyList<string> keys)
    {
        var ids = new HashSet<int>(keys.Count);
        foreach (var key in keys)
        {
            _ = ids.Add(store.Names.Intern(key).Id);
        }

        return ids;
    }

    /// <summary>Gets the kind of a value after following references.</summary>
    /// <param name="raw">The raw value.</param>
    /// <returns>The kind; null when the object cannot be read.</returns>
    private PdfKind KindOf(PdfValue raw)
    {
        try
        {
            return StoreReading.Resolve(_store, raw).Kind;
        }
        catch (PdfException)
        {
            return PdfKind.Null;
        }
    }

    /// <summary>Checks a value and what it refers to, within fixed limits.</summary>
    /// <param name="raw">The raw value.</param>
    /// <returns><see langword="true"/> when no reference leads to a missing or unreadable object.</returns>
    private bool ReadsCleanly(PdfValue raw)
    {
        var budget = MaxCheckValues;
        try
        {
            return Check(raw, 0, ref budget);
        }
        catch (PdfException)
        {
            return false;
        }
    }

    /// <summary>Checks one value and its children.</summary>
    /// <param name="raw">The raw value.</param>
    /// <param name="depth">The depth.</param>
    /// <param name="budget">The values still allowed.</param>
    /// <returns><see langword="true"/> when clean.</returns>
    private bool Check(PdfValue raw, int depth, ref int budget)
    {
        budget--;
        var value = StoreReading.Resolve(_store, raw);
        if (raw.IsReference && value.IsNull)
        {
            return false;
        }

        if (depth >= MaxCheckDepth || budget <= 0)
        {
            return true;
        }

        return CheckChildren(value, depth, ref budget);
    }

    /// <summary>Checks the elements of an array or the values of a dictionary.</summary>
    /// <param name="value">The resolved value.</param>
    /// <param name="depth">The value's depth.</param>
    /// <param name="budget">The values still allowed.</param>
    /// <returns><see langword="true"/> when every child is clean.</returns>
    private bool CheckChildren(PdfValue value, int depth, ref int budget)
    {
        if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                if (!Check(array.GetRaw(i), depth + 1, ref budget))
                {
                    return false;
                }
            }
        }

        var dictionary = value.AsDictionary();
        for (var i = 0; dictionary is not null && i < dictionary.Count; i++)
        {
            if (!Check(dictionary.GetValueAt(i), depth + 1, ref budget))
            {
                return false;
            }
        }

        return true;
    }
}
