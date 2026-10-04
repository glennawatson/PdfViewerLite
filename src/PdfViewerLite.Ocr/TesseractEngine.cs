// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Ocr.Native;

namespace PdfViewerLite.Ocr;

/// <summary>
/// Recognises text with Tesseract, loaded from the app directory or the system. When Tesseract or its language data is
/// missing the engine reports <see cref="IsAvailable"/> as <see langword="false"/> and recognises nothing.
/// </summary>
[DebuggerDisplay("Tesseract {Language} available={IsAvailable}")]
public sealed unsafe class TesseractEngine : IOcrEngine
{
    /// <summary>Tesseract's word level.</summary>
    private const int WordLevel = 3;

    /// <summary>Tesseract's automatic page segmentation, which finds columns and blocks.</summary>
    private const int AutomaticSegmentation = 3;

    /// <summary>The points in an inch.</summary>
    private const float PointsPerInch = 72;

    /// <summary>Words recognised with less confidence than this are noise, not text.</summary>
    private const float MinimumConfidence = 20;

    /// <summary>The environment variable Tesseract itself uses for its language data.</summary>
    private const string DataVariable = "TESSDATA_PREFIX";

    /// <summary>Where distributions install the language data.</summary>
    private static readonly string[] DataDirectories =
    [
        "/usr/share/tesseract-ocr/5/tessdata",
        "/usr/share/tessdata",
        "/usr/share/tesseract-ocr/4.00/tessdata",
        "/usr/local/share/tessdata",
        "/opt/homebrew/share/tessdata",
        @"C:\Program Files\Tesseract-OCR\tessdata",
    ];

    /// <summary>Serialises use of the engine.</summary>
    private readonly Lock _gate = new();

    /// <summary>The engine, or <see langword="null"/> when unavailable.</summary>
    private readonly TesseractHandle? _handle;

    /// <summary>Initializes a new instance of the <see cref="TesseractEngine"/> class.</summary>
    /// <param name="language">The Tesseract language, for example <c>eng</c> or <c>eng+deu</c>.</param>
    public TesseractEngine(string language)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(language);
        Language = language;
        TesseractLibraryResolver.Install();
        if (!TesseractLibraryResolver.TryLoad() || FindDataDirectory(language) is not { } directory)
        {
            return;
        }

        var handle = NativeMethods.TessBaseAPICreate();
        if (handle.IsInvalid || !Initialize(handle, directory, language))
        {
            handle.Dispose();
            return;
        }

        NativeMethods.TessBaseAPISetPageSegMode(handle, AutomaticSegmentation);
        _handle = handle;
    }

    /// <summary>Gets the language the engine recognises.</summary>
    public string Language { get; }

    /// <inheritdoc/>
    public bool IsAvailable => _handle is not null;

    /// <summary>Finds the directory holding the language data for every language in a Tesseract language string.</summary>
    /// <param name="language">The language, for example <c>eng+deu</c>.</param>
    /// <returns>The directory, or <see langword="null"/> when the data is not installed.</returns>
    public static string? FindDataDirectory(string language)
    {
        ArgumentNullException.ThrowIfNull(language);
        var configured = Environment.GetEnvironmentVariable(DataVariable);
        if (!string.IsNullOrEmpty(configured) && HasLanguages(configured, language))
        {
            return configured;
        }

        var local = Path.Combine(AppContext.BaseDirectory, "tessdata");
        if (HasLanguages(local, language))
        {
            return local;
        }

        foreach (var directory in DataDirectories)
        {
            if (HasLanguages(directory, language))
            {
                return directory;
            }
        }

        return null;
    }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _handle?.Dispose();

    /// <inheritdoc/>
    public void Recognize(ReadOnlySpan<byte> image, int width, int height, float pixelsPerPoint, List<OcrWord> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(width);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(height);
        ArgumentOutOfRangeException.ThrowIfNegativeOrZero(pixelsPerPoint);
        ArgumentOutOfRangeException.ThrowIfLessThan(image.Length, width * height);
        if (_handle is null)
        {
            return;
        }

        lock (_gate)
        {
            fixed (byte* pixels = image)
            {
                NativeMethods.TessBaseAPISetImage(_handle, pixels, width, height, 1, width);
                NativeMethods.TessBaseAPISetSourceResolution(_handle, (int)MathF.Round(pixelsPerPoint * PointsPerInch));
                if (NativeMethods.TessBaseAPIRecognize(_handle, 0) == 0)
                {
                    ReadWords(_handle, pixelsPerPoint, output);
                }

                NativeMethods.TessBaseAPIClear(_handle);
            }
        }
    }

    /// <summary>Reads the recognised words.</summary>
    /// <param name="handle">The engine, after recognition.</param>
    /// <param name="pixelsPerPoint">The image resolution.</param>
    /// <param name="output">The list receiving the words.</param>
    private static void ReadWords(TesseractHandle handle, float pixelsPerPoint, List<OcrWord> output)
    {
        using var iterator = NativeMethods.TessBaseAPIGetIterator(handle);
        if (iterator.IsInvalid)
        {
            return;
        }

        var page = NativeMethods.TessResultIteratorGetPageIterator(iterator);
        do
        {
            var confidence = NativeMethods.TessResultIteratorConfidence(iterator, WordLevel);
            if (confidence < MinimumConfidence
                || NativeMethods.TessPageIteratorBoundingBox(page, WordLevel, out var left, out var top, out var right, out var bottom) == 0)
            {
                continue;
            }

            var text = NativeMethods.TessResultIteratorGetUTF8Text(iterator, WordLevel);
            if (text is null)
            {
                continue;
            }

            try
            {
                var utf8 = MemoryMarshal.CreateReadOnlySpanFromNullTerminated(text).Trim(" \t\r\n"u8);
                if (!utf8.IsEmpty)
                {
                    var bounds = PageRect.FromEdges(left / pixelsPerPoint, top / pixelsPerPoint, right / pixelsPerPoint, bottom / pixelsPerPoint);
                    output.Add(new(Encoding.UTF8.GetString(utf8), bounds, confidence));
                }
            }
            finally
            {
                NativeMethods.TessDeleteText(text);
            }
        }
        while (NativeMethods.TessResultIteratorNext(iterator, WordLevel) != 0);
    }

    /// <summary>Initialises the engine with a language.</summary>
    /// <param name="handle">The engine.</param>
    /// <param name="directory">The language data directory.</param>
    /// <param name="language">The language.</param>
    /// <returns><see langword="true"/> on success.</returns>
    private static bool Initialize(TesseractHandle handle, string directory, string language)
    {
        var directoryBytes = Encoding.UTF8.GetBytes(directory + '\0');
        var languageBytes = Encoding.UTF8.GetBytes(language + '\0');
        fixed (byte* directoryPointer = directoryBytes)
        {
            fixed (byte* languagePointer = languageBytes)
            {
                return NativeMethods.TessBaseAPIInit3(handle, directoryPointer, languagePointer) == 0;
            }
        }
    }

    /// <summary>Determines whether a directory holds the data for every language in a language string.</summary>
    /// <param name="directory">The directory.</param>
    /// <param name="language">The languages separated by <c>+</c>.</param>
    /// <returns><see langword="true"/> when all are present.</returns>
    private static bool HasLanguages(string directory, string language)
    {
        foreach (var range in language.AsSpan().Split('+'))
        {
            if (!File.Exists(Path.Combine(directory, $"{language.AsSpan()[range]}.traineddata")))
            {
                return false;
            }
        }

        return true;
    }
}
