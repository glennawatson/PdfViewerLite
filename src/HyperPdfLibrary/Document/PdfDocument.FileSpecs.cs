// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>File specifications, URIs, launch actions and scripts.</content>
public sealed partial class PdfDocument
{
    /// <summary>The position of the drive letter's closing slash in "/c/dir/file".</summary>
    private const int DriveSlot = 2;

    /// <summary>The most bytes kept of a script stream.</summary>
    private const int MaxScriptLength = 1 << 20;

    /// <summary>Gets the spelling of the /DOS file specification key.</summary>
    private static ReadOnlySpan<byte> DosSpelling => "DOS"u8;

    /// <summary>Gets the spelling of the /FS file system key.</summary>
    private static ReadOnlySpan<byte> FsSpelling => "FS"u8;

    /// <summary>Gets the spelling of the /URL file system name.</summary>
    private static ReadOnlySpan<byte> UrlSpelling => "URL"u8;

    /// <summary>
    /// Converts a path written in PDF file specification syntax to the platform's. On Windows "/c/dir/file" becomes
    /// "c:\dir\file" and "//server/share" becomes "\\server\share"; elsewhere the syntax is already the platform's.
    /// </summary>
    /// <param name="path">The path as written in the file specification.</param>
    /// <returns>The platform path.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string ToPlatformPath(string path) => ToPlatformPath(path, OperatingSystem.IsWindows());

    /// <summary>Converts a file specification path to Windows or POSIX syntax.</summary>
    /// <param name="path">The path as written in the file specification.</param>
    /// <param name="windows">Whether to produce a Windows path.</param>
    /// <returns>The path.</returns>
    internal static string ToPlatformPath(string path, bool windows)
    {
        if (!windows || path.Length == 0)
        {
            return path;
        }

        var converted = path.Replace('/', '\\');

        // "//server/share" is already a UNC path once the slashes are flipped.
        if (path[0] != '/' || (path.Length > 1 && path[1] == '/'))
        {
            return converted;
        }

        return path.Length > DriveSlot && path[DriveSlot] == '/' ? string.Concat(path.AsSpan(1, 1), ":", converted.AsSpan(DriveSlot)) : converted;
    }

    /// <summary>Reads a script held as a string or a stream, keeping at most <see cref="MaxScriptLength"/> bytes of a stream.</summary>
    /// <param name="value">The /JS value.</param>
    /// <returns>The script, or <see langword="null"/>.</returns>
    internal static string? ReadScript(PdfValue value)
    {
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        return value.AsStream() is { } stream ? DecodeScript(stream) : null;
    }

    /// <summary>Reads a file specification: a string, or a dictionary preferring /UF, then /F, /DOS, /Mac and /Unix.</summary>
    /// <param name="value">The file specification.</param>
    /// <returns>The file name as written, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string? ReadFileSpec(PdfValue value) => ReadFileSpec(value, out _);

    /// <summary>Reads a file specification, noting whether it is a URL (/FS /URL).</summary>
    /// <param name="value">The file specification.</param>
    /// <param name="isUrl">Whether the specification names a URL rather than a file.</param>
    /// <returns>The file name or URL as written, or <see langword="null"/>.</returns>
    internal string? ReadFileSpec(PdfValue value, out bool isUrl)
    {
        isUrl = false;
        if (value.Kind == PdfKind.String)
        {
            return PdfText.Decode(value.AsStringBytes());
        }

        if (value.AsDictionary() is not { } spec)
        {
            return null;
        }

        isUrl = IsUrlSpec(spec);
        var name = spec.GetText(KnownName.UF);
        name = string.IsNullOrEmpty(name) ? spec.GetText(KnownName.F) : name;
        if (string.IsNullOrEmpty(name) && !isUrl)
        {
            name = spec.GetText(Objects.Names.Intern(DosSpelling)) ?? spec.GetText(KnownName.Mac) ?? spec.GetText(KnownName.Unix);
        }

        return string.IsNullOrEmpty(name) ? null : name;
    }

    /// <summary>Decodes a script stream and keeps its first <see cref="MaxScriptLength"/> bytes.</summary>
    /// <param name="stream">The script stream.</param>
    /// <returns>The script.</returns>
    private static string DecodeScript(PdfStream stream)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref output);
            return PdfText.Decode(output.WrittenSpan[..Math.Min(output.Length, MaxScriptLength)]);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Determines whether a file specification dictionary has /FS /URL.</summary>
    /// <param name="spec">The file specification.</param>
    /// <returns><see langword="true"/> for a URL.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool IsUrlSpec(PdfDictionary spec) =>
        Objects.Names.NameEquals(spec.GetName(Objects.Names.Intern(FsSpelling)), UrlSpelling);

    /// <summary>Reads a URI action; a URI without a scheme is resolved against the catalog's /URI /Base, as in PDFium.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action.</returns>
    private UriAction ReadUri(PdfDictionary action)
    {
        var uri = action.GetStringBytes(KnownName.URI);
        var text = Encoding.UTF8.GetString(uri);
        var hasScheme = uri.IndexOf((byte)':') >= 1;
        return hasScheme || Catalog.GetDictionary(KnownName.URI) is not { } settings
            ? new(text)
            : new(Encoding.UTF8.GetString(settings.GetStringBytes(KnownName.Base)) + text);
    }

    /// <summary>Reads a launch action, preferring /F and then the Windows /Win /F.</summary>
    /// <param name="action">The action dictionary.</param>
    /// <returns>The action, or no action when the file is missing.</returns>
    private PdfAction ReadLaunch(PdfDictionary action)
    {
        var file = ReadFileSpec(action.Get(KnownName.F), out var isUrl) ?? ReadFileSpec(action.GetDictionary(KnownName.Win)?.Get(KnownName.F) ?? default, out isUrl);
        if (file is null)
        {
            return default;
        }

        return isUrl ? new UriAction(file) : new LaunchAction(ToPlatformPath(file));
    }
}
