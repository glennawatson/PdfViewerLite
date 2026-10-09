// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <content>The page-resource part of the scan: fonts, and device colour when the file has no output intent.</content>
internal sealed partial class ConformanceScan
{
    /// <summary>Determines whether a font should carry an embedded program but does not. Type 3 fonts draw their own glyphs and are skipped.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <returns><see langword="true"/> when the font has no <c>/FontFile</c>, <c>/FontFile2</c> or <c>/FontFile3</c>.</returns>
    private static bool IsNotEmbedded(PdfDictionary font)
    {
        if (font.IsName(KnownName.Subtype, KnownName.Type3))
        {
            return false;
        }

        var descriptorOwner = font.IsName(KnownName.Subtype, KnownName.Type0) ? font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0) : font;
        return !FontDescriptor.Read(descriptorOwner?.GetDictionary(KnownName.FontDescriptor)).IsEmbedded;
    }

    /// <summary>Determines whether a colour space value names a device space.</summary>
    /// <param name="space">The value.</param>
    /// <returns><see langword="true"/> for DeviceGray, DeviceRGB and DeviceCMYK.</returns>
    private static bool IsDeviceSpace(PdfValue space) =>
        space.IsName(KnownName.DeviceGray) || space.IsName(KnownName.DeviceRGB) || space.IsName(KnownName.DeviceCMYK);

    /// <summary>Determines whether an operator sets a device colour or selects a device colour space.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="firstName">The first operand as a known name, for the colour space operators.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsDeviceColorOperator(ContentOperator op, KnownName firstName) => op switch
    {
        ContentOperator.SetFillGray or ContentOperator.SetStrokeGray
            or ContentOperator.SetFillRgb or ContentOperator.SetStrokeRgb
            or ContentOperator.SetFillCmyk or ContentOperator.SetStrokeCmyk => true,
        ContentOperator.SetFillColorSpace or ContentOperator.SetStrokeColorSpace
            => firstName is KnownName.DeviceGray or KnownName.DeviceRGB or KnownName.DeviceCMYK,
        _ => false,
    };

    /// <summary>Walks the resources of every page, its forms, patterns and annotation appearances.</summary>
    /// <param name="lookForDeviceColor">Whether to look for device colour; it costs a read of the content, so it runs only without an output intent.</param>
    private void ScanPages(bool lookForDeviceColor)
    {
        for (var i = 0; i < _document.PageCount; i++)
        {
            PdfOpenContext.ThrowIfCancelled(_document.Objects.Context);
            var page = _document.GetPage(i);
            ScanResources(page.Resources, lookForDeviceColor, 0);
            if (lookForDeviceColor && !_usesDeviceColor)
            {
                _usesDeviceColor = ContentUsesDeviceColor(page.Dictionary.Get(KnownName.Contents));
            }

            ScanAnnotations(page, lookForDeviceColor);
        }
    }

    /// <summary>Walks the appearance streams of a page's annotations.</summary>
    /// <param name="page">The page.</param>
    /// <param name="lookForDeviceColor">Whether to look for device colour.</param>
    private void ScanAnnotations(PdfPage page, bool lookForDeviceColor)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            var normal = annotations.GetDictionary(i)?.GetDictionary(KnownName.AP)?.Get(KnownName.N);
            if (normal?.AsStream() is { } stream)
            {
                ScanForm(stream, lookForDeviceColor, 1);
            }
            else if (normal?.AsDictionary() is { } states)
            {
                ScanAppearanceStates(states, lookForDeviceColor);
            }
        }
    }

    /// <summary>Walks the appearance streams of an annotation that has one per state.</summary>
    /// <param name="states">The <c>/N</c> dictionary.</param>
    /// <param name="lookForDeviceColor">Whether to look for device colour.</param>
    private void ScanAppearanceStates(PdfDictionary states, bool lookForDeviceColor)
    {
        for (var i = 0; i < states.Count; i++)
        {
            if (_document.Objects.Resolve(states.GetValueAt(i)).AsStream() is { } stream)
            {
                ScanForm(stream, lookForDeviceColor, 1);
            }
        }
    }

    /// <summary>Walks a form XObject or appearance stream.</summary>
    /// <param name="form">The stream.</param>
    /// <param name="lookForDeviceColor">Whether to look for device colour.</param>
    /// <param name="depth">The nesting depth.</param>
    private void ScanForm(PdfStream form, bool lookForDeviceColor, int depth)
    {
        if (!_visited.Add(form.Dictionary))
        {
            return;
        }

        ScanResources(form.Dictionary.GetDictionary(KnownName.Resources), lookForDeviceColor, depth);
        if (lookForDeviceColor && !_usesDeviceColor)
        {
            _usesDeviceColor = StreamUsesDeviceColor(form);
        }
    }

    /// <summary>Walks a resource dictionary: fonts, then the forms, images and patterns it names.</summary>
    /// <param name="resources">The resources, or null.</param>
    /// <param name="lookForDeviceColor">Whether to look for device colour.</param>
    /// <param name="depth">The nesting depth.</param>
    private void ScanResources(PdfDictionary? resources, bool lookForDeviceColor, int depth)
    {
        if (resources is null || depth >= PdfLimits.MaxNesting || !_visited.Add(resources))
        {
            return;
        }

        ScanFonts(resources.GetDictionary(KnownName.Font));
        ScanChildren(resources.GetDictionary(KnownName.XObject), lookForDeviceColor, depth);
        ScanChildren(resources.GetDictionary(KnownName.Pattern), lookForDeviceColor, depth);
    }

    /// <summary>Walks the streams of a resource category.</summary>
    /// <param name="category">The XObject or Pattern dictionary, or null.</param>
    /// <param name="lookForDeviceColor">Whether to look for device colour.</param>
    /// <param name="depth">The nesting depth.</param>
    private void ScanChildren(PdfDictionary? category, bool lookForDeviceColor, int depth)
    {
        for (var i = 0; category is not null && i < category.Count; i++)
        {
            if (_document.Objects.Resolve(category.GetValueAt(i)).AsStream() is not { } stream)
            {
                continue;
            }

            if (stream.Dictionary.IsName(KnownName.Subtype, KnownName.Image))
            {
                _usesDeviceColor |= lookForDeviceColor && IsDeviceSpace(stream.Dictionary.Get(KnownName.ColorSpace));
            }
            else
            {
                ScanForm(stream, lookForDeviceColor, depth + 1);
            }
        }
    }

    /// <summary>Records the fonts of a <c>/Font</c> resource dictionary that have no embedded program.</summary>
    /// <param name="fonts">The dictionary, or null.</param>
    private void ScanFonts(PdfDictionary? fonts)
    {
        for (var i = 0; fonts is not null && i < fonts.Count; i++)
        {
            if (_document.Objects.Resolve(fonts.GetValueAt(i)).AsDictionary() is { } font && _visited.Add(font) && IsNotEmbedded(font))
            {
                _fonts.Add(_document.Objects.Names.GetString(font.GetName(KnownName.BaseFont)) is { Length: > 0 } baseFont ? baseFont : _document.Objects.Names.GetString(fonts.GetKeyAt(i)));
            }
        }
    }

    /// <summary>Reads content streams and stops at the first operator that sets a device colour.</summary>
    /// <param name="contents">A stream, or an array of streams.</param>
    /// <returns><see langword="true"/> when the content uses device colour.</returns>
    private bool ContentUsesDeviceColor(PdfValue contents)
    {
        if (contents.AsStream() is { } single)
        {
            return StreamUsesDeviceColor(single);
        }

        var parts = contents.AsArray();
        for (var i = 0; parts is not null && i < parts.Count; i++)
        {
            if (parts.Get(i).AsStream() is { } part && StreamUsesDeviceColor(part))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Decodes one content stream and looks for device colour operators. A damaged stream counts as no device colour.</summary>
    /// <param name="stream">The stream.</param>
    /// <returns><see langword="true"/> when the content uses device colour.</returns>
    private bool StreamUsesDeviceColor(PdfStream stream)
    {
        var buffer = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref buffer);
            return ReadsDeviceColor(buffer.WrittenSpan);
        }
        catch (Exception ex) when (ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException
            or IndexOutOfRangeException or NotSupportedException or FormatException or OverflowException)
        {
            return false;
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Scans decoded content for <c>g G rg RG k K</c> and <c>cs CS</c> with a device space.</summary>
    /// <param name="content">The decoded content.</param>
    /// <returns><see langword="true"/> at the first such operator.</returns>
    private bool ReadsDeviceColor(ReadOnlySpan<byte> content)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, _document.Objects.Names, operands);
        while (reader.Next(out var op))
        {
            if (IsDeviceColorOperator(op, reader.Operand(0).Name.ToKnownName()))
            {
                return true;
            }
        }

        return false;
    }
}
