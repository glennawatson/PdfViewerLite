// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Text;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>
/// Lists a page's marked content operators in the order the content interpreter runs them, following form XObjects,
/// and reads their property lists, including inline ones. The interpreter passes only named property lists to its
/// device, so <see cref="MarkedContentRecorder"/> takes the inline ones from here.
/// </summary>
internal static class MarkedContentScanner
{
    /// <summary>The newline written between the streams of a content array, as the interpreter writes it.</summary>
    private const byte StreamSeparator = (byte)'\n';

    /// <summary>Gets the key of a marked content id.</summary>
    private static ReadOnlySpan<byte> McidKey => "/MCID"u8;

    /// <summary>Gets the key of replacement text.</summary>
    private static ReadOnlySpan<byte> ActualTextKey => "/ActualText"u8;

    /// <summary>Gets the artifact tag.</summary>
    private static ReadOnlySpan<byte> ArtifactTag => "Artifact"u8;

    /// <summary>Lists a page's marked content operators.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <param name="output">Receives the operators in running order.</param>
    /// <returns><see langword="true"/> when the page paints an image, decodable or not.</returns>
    internal static bool Scan(PdfDocument document, PdfPage page, List<ScannedMark> output)
    {
        var content = default(PooledBuffer);
        try
        {
            AppendContents(page.Dictionary.Get(KnownName.Contents), ref content);
            List<PdfDictionary?> resources = [page.Resources];
            return ScanStream(document, content.WrittenSpan, resources, 0, output);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Decodes a page's <c>/Contents</c>, a stream or an array of streams, into one buffer.</summary>
    /// <param name="contents">The <c>/Contents</c> value.</param>
    /// <param name="buffer">Receives the content.</param>
    private static void AppendContents(PdfValue contents, ref PooledBuffer buffer)
    {
        if (contents.AsStream() is { } single)
        {
            _ = single.Decode(ref buffer);
            return;
        }

        var array = contents.AsArray();
        for (var i = 0; array is not null && i < array.Count; i++)
        {
            if (array.Get(i).AsStream() is not { } part)
            {
                continue;
            }

            var piece = default(PooledBuffer);
            try
            {
                _ = part.Decode(ref piece);
                buffer.Write(piece.WrittenSpan);
                buffer.WriteByte(StreamSeparator);
            }
            finally
            {
                piece.Dispose();
            }
        }
    }

    /// <summary>Scans one content stream.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">The decoded content.</param>
    /// <param name="resources">The resource dictionaries in force, innermost last.</param>
    /// <param name="depth">The form nesting depth.</param>
    /// <param name="output">Receives the operators.</param>
    /// <returns><see langword="true"/> when the stream paints an image.</returns>
    private static bool ScanStream(PdfDocument document, ReadOnlySpan<byte> content, List<PdfDictionary?> resources, int depth, List<ScannedMark> output)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, document.Objects.Names, operands);
        var images = false;
        while (reader.Next(out var op))
        {
            switch (op)
            {
                case ContentOperator.BeginMarkedContent:
                {
                    output.Add(new(reader.Operand(0).Name, -1, null));
                    break;
                }

                case ContentOperator.BeginMarkedContentProperties:
                {
                    output.Add(ReadMark(document, content, reader.Operand(0).Name, reader.Operand(1), resources));
                    break;
                }

                case ContentOperator.PaintXObject:
                {
                    images |= ScanXObject(document, reader.Operand(0).Name, resources, depth, output);
                    break;
                }

                case ContentOperator.BeginInlineImage:
                {
                    images = true;
                    break;
                }

                default:
                {
                    break;
                }
            }
        }

        return images;
    }

    /// <summary>Reads a <c>BDC</c>'s property list.</summary>
    /// <param name="document">The document.</param>
    /// <param name="content">The content the operator is in.</param>
    /// <param name="tag">The tag.</param>
    /// <param name="properties">The property list operand: a name or an inline dictionary.</param>
    /// <param name="resources">The resource dictionaries in force.</param>
    /// <returns>The mark.</returns>
    private static ScannedMark ReadMark(PdfDocument document, ReadOnlySpan<byte> content, PdfName tag, ContentOperand properties, List<PdfDictionary?> resources)
    {
        if (properties.Kind == ContentOperandKind.Name)
        {
            var named = FindResource(resources, KnownName.Properties, properties.Name).AsDictionary();
            return new(tag, named?.GetInt32(KnownName.MCID, -1) ?? -1, named);
        }

        if (properties.Kind != ContentOperandKind.Dictionary || (uint)(properties.Start + properties.Length) > (uint)content.Length)
        {
            return new(tag, -1, null);
        }

        var inline = content.Slice(properties.Start, properties.Length);

        // Most property lists hold only an /MCID; parse in full only when the rest may matter.
        var full = document.Objects.Names.NameEquals(tag, ArtifactTag) || inline.IndexOf(ActualTextKey) >= 0;
        if (!full)
        {
            return new(tag, ScanMcid(inline), null);
        }

        var parsed = new PdfParser(inline.ToArray(), 0, document.Objects, document.Objects.Names).ParseValue().AsDictionary();
        return new(tag, parsed?.GetInt32(KnownName.MCID, -1) ?? -1, parsed);
    }

    /// <summary>Reads the integer after <c>/MCID</c> in an inline property list without parsing it.</summary>
    /// <param name="inline">The property list's bytes.</param>
    /// <returns>The id, or -1.</returns>
    private static int ScanMcid(ReadOnlySpan<byte> inline)
    {
        var at = inline.IndexOf(McidKey);
        if (at < 0)
        {
            return -1;
        }

        var rest = inline[(at + McidKey.Length)..];
        var start = rest.IndexOfAnyExcept(" \t\r\n\f\0"u8);
        return start >= 0 && Utf8Parser.TryParse(rest[start..], out int mcid, out _) && mcid >= 0 ? mcid : -1;
    }

    /// <summary>Scans an XObject that <c>Do</c> paints, as the interpreter would run it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The XObject's resource name.</param>
    /// <param name="resources">The resource dictionaries in force.</param>
    /// <param name="depth">The form nesting depth.</param>
    /// <param name="output">Receives the operators.</param>
    /// <returns><see langword="true"/> when the XObject is an image or a form that paints one.</returns>
    private static bool ScanXObject(PdfDocument document, PdfName name, List<PdfDictionary?> resources, int depth, List<ScannedMark> output)
    {
        if (depth >= PdfLimits.MaxDrawDepth || FindResource(resources, KnownName.XObject, name).AsStream() is not { } xobject)
        {
            return false;
        }

        var dictionary = xobject.Dictionary;
        var membership = dictionary.GetRaw(KnownName.OC);
        if (!membership.IsNull && !document.OptionalContent.IsVisible(membership))
        {
            return false;
        }

        return dictionary.IsName(KnownName.Subtype, KnownName.Form)
            ? ScanForm(document, xobject, resources, depth, output)
            : dictionary.IsName(KnownName.Subtype, KnownName.Image);
    }

    /// <summary>Scans a form XObject's content with its own resources.</summary>
    /// <param name="document">The document.</param>
    /// <param name="form">The form.</param>
    /// <param name="resources">The resource dictionaries in force.</param>
    /// <param name="depth">The form nesting depth.</param>
    /// <param name="output">Receives the operators.</param>
    /// <returns><see langword="true"/> when the form paints an image.</returns>
    private static bool ScanForm(PdfDocument document, PdfStream form, List<PdfDictionary?> resources, int depth, List<ScannedMark> output)
    {
        var content = default(PooledBuffer);
        resources.Add(form.Dictionary.GetDictionary(KnownName.Resources) ?? resources[^1]);
        try
        {
            _ = form.Decode(ref content);
            return ScanStream(document, content.WrittenSpan, resources, depth + 1, output);
        }
        finally
        {
            resources.RemoveAt(resources.Count - 1);
            content.Dispose();
        }
    }

    /// <summary>Finds a named resource, innermost resources first.</summary>
    /// <param name="resources">The resource dictionaries in force.</param>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The resource; null when missing.</returns>
    private static PdfValue FindResource(List<PdfDictionary?> resources, KnownName category, PdfName name)
    {
        for (var i = resources.Count - 1; i >= 0; i--)
        {
            var value = resources[i]?.GetDictionary(category)?.Get(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
