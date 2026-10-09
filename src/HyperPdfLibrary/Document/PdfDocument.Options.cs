// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Opening with options.</content>
public sealed partial class PdfDocument
{
    /// <summary>Opens a file with options; <see cref="PdfOpenOptions.Source"/> chooses how the file is read.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file cannot be read, is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(string path, PdfOpenOptions options)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        ArgumentNullException.ThrowIfNull(options);
        options.CancellationToken.ThrowIfCancellationRequested();
        PdfByteSource source;
        try
        {
            source = PdfByteSources.Open(path, options.Source, options.CacheBytes);
        }
        catch (IOException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            throw new PdfException($"The file '{path}' could not be read.", ex);
        }

        return OpenWith(source, true, options);
    }

    /// <summary>Opens a document held in memory with options; the bytes are kept, not copied, and must not change.</summary>
    /// <param name="bytes">The file's bytes.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The bytes are not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(byte[] bytes, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(bytes);
        ArgumentNullException.ThrowIfNull(options);
        return Create(PdfObjectStore.OpenWith(bytes, options));
    }

    /// <summary>
    /// Opens a document read from a seekable stream through a page cache of <see cref="PdfOpenOptions.CacheBytes"/>. The
    /// stream is not disposed with the document and must stay open and unchanged until the document is disposed.
    /// </summary>
    /// <param name="stream">The readable, seekable stream.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="ArgumentException">The stream cannot read or seek.</exception>
    /// <exception cref="PdfException">The stream is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(Stream stream, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(stream);
        ArgumentNullException.ThrowIfNull(options);
        return OpenWith(new StreamPdfByteSource(stream, options.CacheBytes, false), true, options);
    }

    /// <summary>Opens a document read from a byte source.</summary>
    /// <param name="source">The file.</param>
    /// <param name="ownsSource">Whether the document disposes the source, including when opening fails.</param>
    /// <param name="options">The options.</param>
    /// <returns>The document.</returns>
    /// <exception cref="PdfException">The file is not a PDF, or the password is wrong.</exception>
    /// <exception cref="OperationCanceledException">The options' token was cancelled.</exception>
    public static PdfDocument OpenWith(PdfByteSource source, bool ownsSource, PdfOpenOptions options)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        return Create(PdfObjectStore.OpenWith(source, ownsSource, options));
    }
}
