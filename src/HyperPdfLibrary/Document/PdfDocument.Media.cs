// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Screen, movie, sound and rendition data. Everything is read as data; nothing plays.</content>
public sealed partial class PdfDocument
{
    /// <summary>The deepest chain of clip sections followed.</summary>
    private const int MaxClipDepth = 8;

    /// <summary>The default sampling rate of a sound, in samples per second.</summary>
    private const double DefaultSoundRate = 44_100;

    /// <summary>The default bits per sample of a sound.</summary>
    private const int DefaultSoundBits = 8;

    /// <summary>The play mode of a movie that names none.</summary>
    private const string DefaultMovieMode = "Once";

    /// <summary>The sound encoding that a sound names none for.</summary>
    private const string DefaultSoundEncoding = "Raw";

    /// <summary>Gets the multimedia annotations (screen, movie, sound, rich media and 3D) of a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The annotations in page order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfMultimediaAnnotation[] GetMultimediaAnnotations(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var annots = page.Dictionary.GetArray(KnownName.Annots);
        var result = new List<PdfMultimediaAnnotation>();
        for (var i = 0; annots is not null && i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is { } annot && MultimediaKindOf(annot.GetName(KnownName.Subtype).ToKnownName()) is { } kind)
            {
                result.Add(ReadMultimedia(annot, kind, page.Index));
            }
        }

        return [.. result];
    }

    /// <summary>Gets the multimedia annotations of every page.</summary>
    /// <returns>The annotations in document order.</returns>
    public PdfMultimediaAnnotation[] GetMultimediaAnnotations()
    {
        var result = new List<PdfMultimediaAnnotation>();
        foreach (var page in PageSet.Pages)
        {
            result.AddRange(GetMultimediaAnnotations(page));
        }

        return [.. result];
    }

    /// <summary>Reads a sound stream.</summary>
    /// <param name="stream">The sound stream.</param>
    /// <returns>The sound.</returns>
    internal static PdfSound ReadSound(PdfStream stream)
    {
        var dictionary = stream.Dictionary;
        return new(
            dictionary.Num("R", DefaultSoundRate),
            dictionary.Int("C", 1),
            dictionary.Int("B", DefaultSoundBits),
            dictionary.NameText("E") ?? DefaultSoundEncoding,
            dictionary.NameText("CO"),
            stream);
    }

    /// <summary>Reads a rendition dictionary.</summary>
    /// <param name="rendition">The rendition dictionary.</param>
    /// <returns>The rendition.</returns>
    internal PdfRendition ReadRendition(PdfDictionary rendition)
    {
        var clip = rendition.Dict("C");
        return new(
            rendition.NameText("S") ?? string.Empty,
            rendition.Text("N"),
            clip is null ? null : ReadMediaClip(clip, 0),
            rendition.Dict("P"),
            rendition.Dict("SP"),
            rendition.Dict("MH"),
            rendition.Dict("BE"));
    }

    /// <summary>Reads a media clip, following clip sections to the clip they are sections of.</summary>
    /// <param name="clip">The clip dictionary.</param>
    /// <param name="depth">The depth of the section chain.</param>
    /// <returns>The clip.</returns>
    internal PdfMediaClip ReadMediaClip(PdfDictionary clip, int depth)
    {
        var subtype = clip.NameText("S") ?? "MCD";
        var data = clip.Value("D");
        if (subtype == "MCS")
        {
            var parent = depth < MaxClipDepth ? data.AsDictionary() : null;
            return new(subtype, clip.Text("N"), null, clip.Text("CT"), null, null, parent is null ? null : ReadMediaClip(parent, depth + 1));
        }

        return new(subtype, clip.Text("N"), data.AsStream() is null ? ReadFileSpec(data) : null, clip.Text("CT"), ClipStream(data), clip.Dict("P")?.Text("TF"), null);
    }

    /// <summary>Gets the media stream of a clip's <c>/D</c> value: the stream itself, or the embedded file of a file specification.</summary>
    /// <param name="data">The <c>/D</c> value.</param>
    /// <returns>The stream, or null when the data is external.</returns>
    private static PdfStream? ClipStream(PdfValue data)
    {
        var embedded = data.AsDictionary()?.GetDictionary(KnownName.EF);
        return data.AsStream() ?? embedded?.GetStream(KnownName.UF) ?? embedded?.GetStream(KnownName.F);
    }

    /// <summary>Converts an array of numbers to integers.</summary>
    /// <param name="array">The array, or null.</param>
    /// <returns>The integers; empty when the array is missing.</returns>
    private static int[] ToInt32Array(PdfArray? array)
    {
        if (array is null)
        {
            return [];
        }

        var values = new int[array.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = array.GetInt32(i);
        }

        return values;
    }

    /// <summary>Gets the multimedia kind of an annotation subtype.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns>The kind, or null when the annotation is not multimedia.</returns>
    private static PdfMultimediaKind? MultimediaKindOf(KnownName subtype) => subtype switch
    {
        KnownName.Screen => PdfMultimediaKind.Screen,
        KnownName.Movie => PdfMultimediaKind.Movie,
        KnownName.Sound => PdfMultimediaKind.Sound,
        KnownName.RichMedia => PdfMultimediaKind.RichMedia,
        KnownName.ThreeD => PdfMultimediaKind.ThreeD,
        _ => null,
    };

    /// <summary>Reads a sound annotation's sound.</summary>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The sound, or null when the annotation has none.</returns>
    private static PdfSound? ReadSoundAnnotation(PdfDictionary annot) => annot.Stream("Sound") is { } sound ? ReadSound(sound) : null;

    /// <summary>Reads a multimedia annotation of a known kind.</summary>
    /// <param name="annot">The annotation dictionary.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The annotation.</returns>
    private PdfMultimediaAnnotation ReadMultimedia(PdfDictionary annot, PdfMultimediaKind kind, int pageIndex)
    {
        PdfRectangle? bounds = annot.TryGetRectangle(KnownName.Rect, out var rect) ? rect : null;
        var empty = new PdfMultimediaAnnotation(kind, pageIndex, bounds, null, null, null, null, null);
        return kind switch
        {
            PdfMultimediaKind.Screen => empty with { Screen = ReadScreen(annot) },
            PdfMultimediaKind.Movie => empty with { Movie = ReadMovie(annot) },
            PdfMultimediaKind.Sound => empty with { Sound = ReadSoundAnnotation(annot) },
            PdfMultimediaKind.RichMedia => empty with { RichMedia = ReadRichMedia(annot) },
            _ => empty with { ThreeD = Read3D(annot) },
        };
    }

    /// <summary>Reads a screen annotation.</summary>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The screen data.</returns>
    private PdfScreenAnnotation ReadScreen(PdfDictionary annot) =>
        new(annot.GetText(KnownName.T), annot.GetDictionary(KnownName.A) is { } action ? ReadActionNode(action) : null, GetTriggers(annot));

    /// <summary>Reads a movie annotation.</summary>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The movie data.</returns>
    private PdfMovieAnnotation ReadMovie(PdfDictionary annot)
    {
        var movie = annot.Dict("Movie");
        var poster = movie?.Value("Poster") ?? default;
        return new(
            annot.GetText(KnownName.T),
            movie is null ? null : ReadFileSpec(movie.Get(KnownName.F)),
            ToInt32Array(movie?.Array("Aspect")),
            movie?.Int("Rotate", 0) ?? 0,
            !poster.IsNull && poster.AsBoolean(true),
            ReadMovieActivation(annot.Dict("A")));
    }

    /// <summary>Reads movie activation parameters; a missing or boolean value gives the defaults.</summary>
    /// <param name="activation">The <c>/A</c> dictionary, or null.</param>
    /// <returns>The activation.</returns>
    private PdfMovieActivation ReadMovieActivation(PdfDictionary? activation) => activation is null
        ? new(null, null, 1, 1, false, DefaultMovieMode, false, [], [])
        : new(
            activation.Value("Start").ScalarText(Objects),
            activation.Value("Duration").ScalarText(Objects),
            activation.Num("Rate", 1),
            activation.Num("Volume", 1),
            activation.Flag("ShowControls", false),
            activation.NameText("Mode") ?? DefaultMovieMode,
            activation.Flag("Synchronous", false),
            ToInt32Array(activation.Array("FWScale")),
            activation.Array("FWPosition").Numbers());
}
