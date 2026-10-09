// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <summary>
/// The indirect objects of a document, parsed on first use and cached. A cached object is read without a lock; parsing a
/// new one takes the store's lock. Safe to call from any thread.
/// </summary>
[DebuggerDisplay("PdfObjectStore: {Size} objects")]
public sealed partial class PdfObjectStore : IDisposable
{
    /// <summary>The file.</summary>
    private readonly PdfByteSource _source;

    /// <summary>The whole file when it is held in one array, which objects are parsed from in place; else <see langword="null"/>.</summary>
    private readonly byte[]? _array;

    /// <summary>Whether disposing the store disposes <see cref="_source"/>.</summary>
    private readonly bool _ownsSource;

    /// <summary>Guards parsing, the object stream cache and edits. Re-entrant, as parsing one object can need another.</summary>
    private readonly Lock _gate = new();

    /// <summary>The most recently used decoded object streams, by object number; others are decoded again when needed.</summary>
    private readonly ObjectStreamCache _objectStreams = new();

    /// <summary>Where each object lives.</summary>
    private XrefTable _xref;

    /// <summary>The parsed objects, by number: a dictionary, array or stream, or a <see cref="ValueBox"/> for other values.</summary>
    private object?[] _cache;

    /// <summary>Whether a damaged table has already been rebuilt.</summary>
    private bool _repairTried;

    /// <summary>1 once disposed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="PdfObjectStore"/> class.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether disposing the store disposes the file.</param>
    /// <param name="xref">Where each object lives.</param>
    private PdfObjectStore(PdfByteSource source, bool ownsSource, XrefTable xref)
    {
        _source = source;
        _array = source.WholeArray;
        _ownsSource = ownsSource;
        _xref = xref;
        _cache = new object?[xref.Size];
    }

    /// <summary>Gets the document's name table.</summary>
    public PdfNameTable Names { get; } = new();

    /// <summary>Gets the security handler, or <see langword="null"/> when the document is not encrypted.</summary>
    public PdfSecurityHandler? Security { get; private set; }

    /// <summary>Gets the trailer dictionary.</summary>
    public PdfDictionary Trailer => _xref.Trailer!;

    /// <summary>Gets the document catalog.</summary>
    public PdfDictionary Catalog { get; private set; } = null!;

    /// <summary>Gets the header version, for example "1.7".</summary>
    public string Version { get; private set; } = string.Empty;

    /// <summary>Gets one more than the highest object number.</summary>
    public int Size => Math.Max(_xref.Size, _nextNumber);

    /// <summary>Gets a value indicating whether the cross-reference table was rebuilt because the file is damaged.</summary>
    public bool WasRepaired => _xref.Repaired;

    /// <summary>Gets a value indicating whether the newest cross-reference section is a stream.</summary>
    public bool UsesXrefStreams => _xref.UsesXrefStreams;

    /// <summary>Gets the offset of the newest cross-reference section, or -1 when the table was rebuilt.</summary>
    public long StartXref => _xref.StartXref;

    /// <summary>Gets the offset of the <c>%PDF-</c> header; offsets in the file are relative to it, as viewers read them.</summary>
    public int HeaderOffset { get; private set; }

    /// <summary>Gets the file the objects are read from.</summary>
    public PdfByteSource Source => _source;

    /// <summary>Gets a value indicating whether the store has been disposed.</summary>
    public bool IsDisposed => Volatile.Read(ref _disposed) != 0;

    /// <summary>Gets the cancellation and diagnostics context, or <see langword="null"/> when none was given.</summary>
    internal PdfOpenContext? Context { get; private set; }

    /// <summary>Gets the amount added to an offset written in the file to find its position: the header offset, or zero.</summary>
    internal int OffsetBase => _xref.OffsetBase;

    /// <summary>Follows a reference, and any chain of references, to its value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The resolved value; null when the object is missing.</returns>
    public PdfValue Resolve(PdfValue value)
    {
        for (var i = 0; value.IsReference && i < PdfLimits.MaxReferenceChain; i++)
        {
            value = GetObject(value.AsReference());
        }

        if (value.IsReference)
        {
            PdfOpenContext.Report(Context, PdfDiagnosticCode.RecursionLimit, "A reference chain was too long and was cut off.", 0, -1);
            return default;
        }

        return value;
    }

    /// <summary>Gets an indirect object.</summary>
    /// <param name="id">The object id; the generation is not checked, as damaged files often get it wrong.</param>
    /// <returns>The value; null when missing.</returns>
    /// <exception cref="ObjectDisposedException">The store has been disposed.</exception>
    public PdfValue GetObject(PdfObjectId id)
    {
        ObjectDisposedException.ThrowIf(Volatile.Read(ref _disposed) != 0, this);
        var number = id.Number;
        var cache = Volatile.Read(ref _cache);
        if ((uint)number < (uint)cache.Length && Volatile.Read(ref cache[number]) is { } cached)
        {
            return Unbox(cached);
        }

        lock (_gate)
        {
            return GetObjectLocked(number);
        }
    }

    /// <summary>
    /// Releases the security handler, the file and the parsed objects. Later reads throw <see cref="ObjectDisposedException"/>;
    /// objects callers already hold stay usable until they are collected.
    /// </summary>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) != 0)
        {
            return;
        }

        lock (_gate)
        {
            // A parse in progress finishes first. Readers that already hold the old array use it until they are done.
            _objectStreams.Clear();
            Volatile.Write(ref _cache, []);
        }

        Security?.Dispose();
        if (_ownsSource)
        {
            _source.Dispose();
        }
    }

    /// <summary>Gets an indirect object as a dictionary, including a stream's dictionary.</summary>
    /// <param name="id">The object id.</param>
    /// <returns>The dictionary, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfDictionary? GetDictionary(PdfObjectId id) => GetObject(id).AsDictionary();

    /// <summary>Converts a cached object back to a value.</summary>
    /// <param name="cached">The cached object.</param>
    /// <returns>The value.</returns>
    private static PdfValue Unbox(object cached) => cached switch
    {
        PdfDictionary dictionary => PdfValue.FromDictionary(dictionary),
        PdfStream stream => PdfValue.FromStream(stream),
        PdfArray array => PdfValue.FromArray(array),
        ValueBox box => box.Value,
        _ => default,
    };

    /// <summary>Wraps a value for the cache.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The object to cache.</returns>
    private static object Box(PdfValue value) => value.Kind switch
    {
        PdfKind.Dictionary => value.AsDictionary()!,
        PdfKind.Stream => value.AsStream()!,
        PdfKind.Array => value.AsArray()!,
        PdfKind.Null => ValueBox.NullValue,
        _ => new ValueBox(value),
    };

    /// <summary>Gets an object while holding the lock.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The value.</returns>
    private PdfValue GetObjectLocked(int number)
    {
        if (TryGetEdited(number, out var edited))
        {
            return edited;
        }

        if ((uint)number < (uint)_cache.Length && _cache[number] is { } cached)
        {
            return Unbox(cached);
        }

        var found = TryLoad(number, out var value);
        if (!found && !_repairTried && Context is not { Recovery: false } && _xref.GetType(number) != XrefEntryType.Free)
        {
            // The table pointed somewhere wrong: rebuild it once from a scan of the file, then try again.
            PdfOpenContext.Report(Context, PdfDiagnosticCode.BrokenObject, "An object was not where the cross-reference table said.", number, _xref.GetLocation(number));
            PdfOpenContext.Report(Context, PdfDiagnosticCode.XrefRebuilt, "The cross-reference table was rebuilt after a broken object.", 0, -1);
            Repair();
            value = Load(number);
        }

        if ((uint)number < (uint)_cache.Length)
        {
            Volatile.Write(ref _cache[number], Box(value));
        }

        return value;
    }

    /// <summary>Parses an object from wherever the table says it is.</summary>
    /// <param name="number">The object number.</param>
    /// <returns>The value; null when missing or unreadable.</returns>
    private PdfValue Load(int number) => TryLoad(number, out var value) ? value : default;

    /// <summary>Parses an object, telling a missing object apart from one whose value is null.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="value">The value; null when not found.</param>
    /// <returns><see langword="false"/> when the table's location does not hold the expected object.</returns>
    private bool TryLoad(int number, out PdfValue value)
    {
        switch (_xref.GetType(number))
        {
            case XrefEntryType.InFile:
            {
                return TryLoadFromFile(number, _xref.GetLocation(number), out value);
            }

            case XrefEntryType.Compressed:
            {
                return TryLoadFromObjectStream(number, (int)_xref.GetLocation(number), _xref.GetDetail(number), out value);
            }

            default:
            {
                value = default;
                return true;
            }
        }
    }

    /// <summary>Parses an object at a file offset, checking it is the object expected.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="offset">The offset.</param>
    /// <param name="value">The value; null when the offset does not hold that object.</param>
    /// <returns><see langword="true"/> when the offset holds the object's header.</returns>
    private bool TryLoadFromFile(int number, long offset, out PdfValue value)
    {
        // Offsets count from the header; some writers count from the start of the file instead.
        if (_xref.OffsetBase != 0 && TryParseObjectAt(offset + _xref.OffsetBase, out var shiftedId, out var shifted) && shiftedId.Number == number)
        {
            value = shifted;
            return true;
        }

        var found = TryParseObjectAt(offset, out var id, out value) && id.Number == number;
        if (!found)
        {
            value = default;
        }

        return found;
    }

    /// <summary>Gets an object packed in an object stream.</summary>
    /// <param name="number">The object number.</param>
    /// <param name="container">The object stream's number.</param>
    /// <param name="index">The object's index in the stream.</param>
    /// <param name="value">The value; null when missing.</param>
    /// <returns><see langword="true"/> when the object stream holds the object.</returns>
    private bool TryLoadFromObjectStream(int number, int container, int index, out PdfValue value)
    {
        if (_objectStreams.TryGetValue(container, out var cached))
        {
            return cached.TryParse(number, index, this, out value);
        }

        // An object stream inside an object stream is invalid, so the container must be a plain object.
        if (_xref.GetType(container) != XrefEntryType.InFile || GetObjectLocked(container).AsStream() is not { } stream)
        {
            value = default;
            return false;
        }

        var objects = ObjectStreamIndex.Read(stream);
        _objectStreams.Add(container, objects);
        return objects.TryParse(number, index, this, out value);
    }

    /// <summary>Rebuilds the cross-reference table by scanning the file, keeping the trailer when the scan finds none.</summary>
    private void Repair()
    {
        _repairTried = true;
        var rebuilt = XrefRepair.Rebuild(_source, this);
        rebuilt.Trailer ??= _xref.Trailer;
        _xref = rebuilt;
        _objectStreams.Clear();

        // Objects already parsed stay cached; only the ones that were missing are looked up in the new table.
        var grown = new object?[Math.Max(rebuilt.Size, _cache.Length)];
        _cache.CopyTo(grown, 0);
        Volatile.Write(ref _cache, grown);
    }

    /// <summary>
    /// Indexes object streams again after the file key is known, for a table rebuilt by scanning, because its object
    /// streams could not be read while they were still encrypted.
    /// </summary>
    private void IndexCompressedAfterAuthentication()
    {
        if (!_xref.Repaired)
        {
            return;
        }

        XrefRepair.IndexCompressed(_xref, this);
        _nextNumber = Math.Max(_nextNumber, _xref.Size);
        if (_cache.Length < _xref.Size)
        {
            Array.Resize(ref _cache, _xref.Size);
        }
    }

    /// <summary>A cached value that is not a dictionary, array or stream.</summary>
    /// <param name="value">The value.</param>
    private sealed class ValueBox(PdfValue value)
    {
        /// <summary>Gets the shared box for null objects.</summary>
        public static ValueBox NullValue { get; } = new(default);

        /// <summary>Gets the value.</summary>
        public PdfValue Value { get; } = value;
    }
}
