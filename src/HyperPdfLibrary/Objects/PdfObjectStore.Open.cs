// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Security.Cryptography.X509Certificates;
using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Security;
using HyperPdfLibrary.Structure;

namespace HyperPdfLibrary.Objects;

/// <content>Opening a file.</content>
public sealed partial class PdfObjectStore
{
    /// <summary>How far into the file the header may appear; some writers put junk first.</summary>
    private const int HeaderSearchLength = 1024;

    /// <summary>The longest version string read from the header.</summary>
    private const int MaxVersionLength = 3;

    /// <summary>Gets the header signature.</summary>
    private static ReadOnlySpan<byte> HeaderSignature => "%PDF-"u8;

    /// <summary>Opens a file's objects.</summary>
    /// <param name="file">The file's bytes; kept, not copied.</param>
    /// <param name="password">The password, or <see langword="null"/>.</param>
    /// <returns>The objects.</returns>
    /// <exception cref="PdfException">The file is not a readable PDF, or the password is wrong.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfObjectStore Open(byte[] file, string? password) => Open(file, password, null, PdfOpenContext.Create(PdfOpenOptions.Default));

    /// <summary>Opens a file's objects with options.</summary>
    /// <param name="file">The file's bytes; kept, not copied.</param>
    /// <param name="options">The options.</param>
    /// <returns>The objects.</returns>
    /// <exception cref="PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfObjectStore OpenWith(byte[] file, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(file);
        ArgumentNullException.ThrowIfNull(options);
        return Open(new MemoryPdfByteSource(file), true, new(options.Password, options.Certificate, PdfOpenContext.Create(options)));
    }

    /// <summary>Opens the objects of a file read from a source.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the store disposes the source, including when opening fails.</param>
    /// <param name="options">The options.</param>
    /// <returns>The objects.</returns>
    /// <exception cref="PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfObjectStore OpenWith(PdfByteSource source, bool ownsSource, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        return Open(source, ownsSource, new(options.Password, options.Certificate, PdfOpenContext.Create(options)));
    }

    /// <summary>Opens a file's objects.</summary>
    /// <param name="file">The file's bytes; kept, not copied.</param>
    /// <param name="password">The password for the standard security handler, or <see langword="null"/>.</param>
    /// <param name="certificate">A recipient's certificate with its private key for the public-key handler, or <see langword="null"/>.</param>
    /// <param name="context">The cancellation and diagnostics context, or <see langword="null"/>.</param>
    /// <returns>The objects.</returns>
    /// <exception cref="PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    private static PdfObjectStore Open(byte[] file, string? password, X509Certificate2? certificate, PdfOpenContext? context)
    {
        ArgumentNullException.ThrowIfNull(file);
        return Open(new MemoryPdfByteSource(file), true, new(password, certificate, context));
    }

    /// <summary>Opens the objects of a file read from a source, disposing an owned source when opening fails.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the store owns the source.</param>
    /// <param name="credentials">The password, certificate and context.</param>
    /// <returns>The objects.</returns>
    /// <exception cref="PdfException">The file is not a readable PDF, or neither the password nor the certificate opens it.</exception>
    private static PdfObjectStore Open(PdfByteSource source, bool ownsSource, OpenCredentials credentials)
    {
        var store = new PdfObjectStore(source, ownsSource, new XrefTable());
        try
        {
            store.Context = credentials.Context;
            store.ReadHeader();
            store.ReadStructure();
            store.Authenticate(credentials.Password, credentials.Certificate);
            store.Catalog = store.FindCatalog() ?? throw new PdfException(PdfError.Format, "The document has no catalog.");
            return store;
        }
        catch
        {
            store.Dispose();
            throw;
        }
    }

    /// <summary>Reads the version digits after the header signature.</summary>
    /// <param name="bytes">The bytes after "%PDF-".</param>
    /// <returns>The version, for example "1.7".</returns>
    private static string ReadVersion(ReadOnlySpan<byte> bytes)
    {
        var length = 0;
        while (length < Math.Min(bytes.Length, MaxVersionLength) && bytes[length] is (>= (byte)'0' and <= (byte)'9') or (byte)'.')
        {
            length++;
        }

        return Encoding.UTF8.GetString(bytes[..length]);
    }

    /// <summary>Throws the exception for a failed authentication.</summary>
    /// <param name="result">The outcome.</param>
    /// <exception cref="PdfException">The outcome is not success.</exception>
    private static void ThrowIfFailed(PdfSecurityResult result)
    {
        switch (result)
        {
            case PdfSecurityResult.UnsupportedHandler:
            {
                throw new PdfException(PdfError.Security, "The document uses an unsupported security handler.");
            }

            case PdfSecurityResult.WrongPassword:
            {
                throw new PdfException(PdfError.Password, "The document is password protected.");
            }

            case PdfSecurityResult.CertificateRequired:
            {
                throw new PdfException(PdfError.Certificate, "The document is encrypted for certificates; open it with a recipient's certificate and private key.");
            }

            case PdfSecurityResult.Damaged:
            {
                throw new PdfException(PdfError.Format, "The document's encryption dictionary is damaged.");
            }

            default:
            {
                return;
            }
        }
    }

    /// <summary>Finds the <c>%PDF-</c> header near the start of the file and reads the version after it.</summary>
    /// <exception cref="PdfException">There is no header.</exception>
    private void ReadHeader()
    {
        using var start = _source.Lease(0, (int)Math.Min(_source.Length, HeaderSearchLength + HeaderSignature.Length + MaxVersionLength));
        var span = start.Span;
        var header = span[..Math.Min(span.Length, HeaderSearchLength)].IndexOf(HeaderSignature);
        if (header < 0)
        {
            throw new PdfException(PdfError.Format, "The file is not a PDF document.");
        }

        HeaderOffset = header;
        if (header > 0)
        {
            PdfOpenContext.Report(Context, PdfDiagnosticCode.HeaderOffset, "The PDF header is not at the start of the file.", 0, header);
        }

        Version = ReadVersion(span[(header + HeaderSignature.Length)..]);
    }

    /// <summary>Reads the cross-reference sections, rebuilding them by scanning when they are damaged.</summary>
    /// <exception cref="PdfException">No trailer could be found.</exception>
    private void ReadStructure()
    {
        var table = new XrefTable();
        _xref = table;
        if (!XrefReader.TryRead(_source, this, table) || !RootResolves(table))
        {
            if (Context is { Recovery: false })
            {
                throw new PdfException(PdfError.Format, "The cross-reference table is missing or damaged and recovery is off.");
            }

            _repairTried = true;
            PdfOpenContext.Report(Context, PdfDiagnosticCode.XrefRebuilt, "The cross-reference table was missing or wrong and was rebuilt.", 0, -1);
            var rebuilt = XrefRepair.Rebuild(_source, this);
            rebuilt.Trailer ??= table.Trailer;
            _xref = rebuilt;
        }

        if (_xref.Trailer is null)
        {
            throw new PdfException(PdfError.Format, "The document is too damaged to read.");
        }

        _cache = new object?[_xref.Size];
        _nextNumber = _xref.Size;
    }

    /// <summary>Checks that a table's /Root points at a dictionary.</summary>
    /// <param name="table">The table.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private bool RootResolves(XrefTable table)
    {
        var root = table.Trailer?.GetRaw(KnownName.Root) ?? default;
        if (!root.IsReference)
        {
            return root.Kind == PdfKind.Dictionary;
        }

        // An encrypted file's object streams cannot be read before the key is known, so trust an entry pointing into one.
        var number = root.AsReference().Number;
        if (table.GetType(number) == XrefEntryType.Compressed && table.Trailer!.ContainsKey(KnownName.Encrypt))
        {
            return true;
        }

        _cache = new object?[table.Size];
        return Load(number).Kind == PdfKind.Dictionary;
    }

    /// <summary>Creates the security handler when the document is encrypted.</summary>
    /// <param name="password">The password.</param>
    /// <param name="certificate">A recipient's certificate, or <see langword="null"/>.</param>
    /// <exception cref="PdfException">The password or certificate does not open the document, or the security handler is unsupported.</exception>
    private void Authenticate(string? password, X509Certificate2? certificate)
    {
        var encrypt = Trailer.GetDictionary(KnownName.Encrypt);
        if (encrypt is null)
        {
            return;
        }

        var firstId = Trailer.GetArray(KnownName.ID) is { } ids ? ids.Get(0).AsStringBytes() : [];
        var result = PdfSecurityHandler.TryCreate(encrypt, firstId, password, certificate, out var handler);
        ThrowIfFailed(result);
        Security = handler;

        // Objects parsed before the key was known hold encrypted strings; parse them again.
        _cache = new object?[_cache.Length];
        _objectStreams.Clear();
        IndexCompressedAfterAuthentication();
        if (!Trailer.GetRaw(KnownName.Encrypt).IsReference)
        {
            return;
        }

        // The /Encrypt dictionary's own strings are never encrypted; keep the copy parsed without a key.
        var reference = Trailer.GetRaw(KnownName.Encrypt).AsReference().Number;
        if ((uint)reference < (uint)_cache.Length)
        {
            _cache[reference] = encrypt;
        }
    }
}
