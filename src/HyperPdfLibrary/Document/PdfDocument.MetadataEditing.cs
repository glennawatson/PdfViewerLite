// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Metadata edits, written to /Info and to the catalog's XMP /Metadata stream when there is one.</content>
public sealed partial class PdfDocument
{
    /// <summary>The XMP date format: ISO 8601 with the offset.</summary>
    private const string XmpDateFormat = "yyyy-MM-dd'T'HH:mm:sszzz";

    /// <summary>The bytes reserved for a formatted PDF date; at least <see cref="PdfDate.MaxFormattedLength"/>.</summary>
    private const int DateBufferLength = 32;

    /// <summary>
    /// Changes the document's metadata in a transaction (the open one, or its own). The /Info dictionary is replaced, or
    /// created and linked from the trailer; matching XMP properties are updated and the rest of the packet is kept.
    /// </summary>
    /// <param name="edit">The changes; <see langword="null"/> properties are left as they are, empty strings remove entries.</param>
    /// <exception cref="ArgumentNullException"><paramref name="edit"/> is <see langword="null"/>.</exception>
    public void SetMetadata(PdfMetadataEdit edit)
    {
        ArgumentNullException.ThrowIfNull(edit);
        RunEdit("Edit document properties", PdfChangeKinds.Metadata, new MetadataEditState(this, edit), static (transaction, state) => state.Document.SetMetadataCore(transaction, state.Edit));
    }

    /// <summary>Formats a date as XMP writes it.</summary>
    /// <param name="value">The date.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string FormatXmpDate(DateTimeOffset value) => value.ToString(XmpDateFormat, CultureInfo.InvariantCulture);

    /// <summary>Sets a text entry, or removes it for an empty string.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The text, or <see langword="null"/> to leave it.</param>
    /// <param name="version">The document's header version.</param>
    private static void SetInfoText(PdfDictionary info, KnownName key, string? value, string version)
    {
        if (value is null)
        {
            return;
        }

        if (value.Length == 0)
        {
            _ = info.Remove(key);
            return;
        }

        info.Set(key, PdfValue.FromString(PdfText.Encode(value, info.GetStringBytes(key), version)));
    }

    /// <summary>Sets a date entry.</summary>
    /// <param name="info">The information dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="value">The date, or <see langword="null"/> to leave it.</param>
    private static void SetInfoDate(PdfDictionary info, KnownName key, DateTimeOffset? value)
    {
        if (value is not { } date)
        {
            return;
        }

        Span<byte> buffer = stackalloc byte[DateBufferLength];
        var length = PdfDate.Format(date, buffer);
        info.Set(key, PdfValue.FromString(buffer[..length].ToArray()));
    }

    /// <summary>Lists the XMP changes an edit makes.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The changes.</returns>
    private static List<XmpChange> GetXmpChanges(PdfMetadataEdit edit)
    {
        var changes = new List<XmpChange>();
        AddXmpChange(changes, XmpProperty.Title, edit.Title);
        AddXmpChange(changes, XmpProperty.Author, edit.Author);
        AddXmpChange(changes, XmpProperty.Subject, edit.Subject);
        AddXmpChange(changes, XmpProperty.Keywords, edit.Keywords);
        AddXmpChange(changes, XmpProperty.Creator, edit.Creator);
        AddXmpChange(changes, XmpProperty.Producer, edit.Producer);
        AddXmpChange(changes, XmpProperty.Created, edit.Created is { } created ? FormatXmpDate(created) : null);
        if (edit.Modified is { } modified)
        {
            AddXmpChange(changes, XmpProperty.Modified, FormatXmpDate(modified));
            AddXmpChange(changes, XmpProperty.MetadataDate, FormatXmpDate(modified));
        }

        return changes;
    }

    /// <summary>Adds a change when the value is set.</summary>
    /// <param name="changes">The changes.</param>
    /// <param name="property">The property.</param>
    /// <param name="value">The value, or <see langword="null"/> to leave it.</param>
    private static void AddXmpChange(List<XmpChange> changes, XmpProperty property, string? value)
    {
        if (value is not null)
        {
            changes.Add(new(property, value));
        }
    }

    /// <summary>Writes the metadata.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="edit">The changes.</param>
    private void SetMetadataCore(PdfEditTransaction transaction, PdfMetadataEdit edit)
    {
        var raw = Objects.Trailer.GetRaw(KnownName.Info);
        var info = Objects.Resolve(raw).AsDictionary()?.Clone() ?? new PdfDictionary(Objects);
        SetInfoText(info, KnownName.Title, edit.Title, Objects.Version);
        SetInfoText(info, KnownName.Author, edit.Author, Objects.Version);
        SetInfoText(info, KnownName.Subject, edit.Subject, Objects.Version);
        SetInfoText(info, KnownName.Keywords, edit.Keywords, Objects.Version);
        SetInfoText(info, KnownName.Creator, edit.Creator, Objects.Version);
        SetInfoText(info, KnownName.Producer, edit.Producer, Objects.Version);
        SetInfoDate(info, KnownName.CreationDate, edit.Created);
        SetInfoDate(info, KnownName.ModDate, edit.Modified);
        if (raw.IsReference)
        {
            transaction.Replace(raw.AsReference(), PdfValue.FromDictionary(info));
        }
        else
        {
            transaction.SetTrailerEntry(KnownName.Info, PdfValue.FromReference(transaction.Add(PdfValue.FromDictionary(info))));
        }

        WriteXmp(transaction, GetXmpChanges(edit));
    }

    /// <summary>Updates the catalog's XMP stream when it has one and the packet can be edited safely.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="changes">The changes.</param>
    private void WriteXmp(PdfEditTransaction transaction, List<XmpChange> changes)
    {
        var raw = Catalog.GetRaw(KnownName.Metadata);
        if (changes.Count == 0 || !raw.IsReference || Objects.Resolve(raw).AsStream() is not { } stream)
        {
            return;
        }

        if (XmpEditor.Apply(stream.DecodeToArray(), System.Runtime.InteropServices.CollectionsMarshal.AsSpan(changes)) is not { } packet)
        {
            return;
        }

        // The packet is written uncompressed, as XMP is meant to be readable by tools that do not parse PDF.
        var dictionary = stream.Dictionary.Clone();
        _ = dictionary.Remove(KnownName.Filter);
        _ = dictionary.Remove(KnownName.DecodeParms);
        _ = dictionary.Remove(KnownName.Length);
        _ = dictionary.Remove(KnownName.DL);
        transaction.Replace(raw.AsReference(), PdfValue.FromStream(new(dictionary, packet)));
    }

    /// <summary>The arguments of a metadata edit.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Edit">The changes.</param>
    [DebuggerDisplay("MetadataEditState: {Edit}")]
    private readonly record struct MetadataEditState(PdfDocument Document, PdfMetadataEdit Edit);
}
