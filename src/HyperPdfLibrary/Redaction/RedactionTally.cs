// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Redaction;

/// <summary>Counts what an apply run removed and remembers which character codes it took out of which fonts.</summary>
[DebuggerDisplay("RedactionTally: {GlyphsRemoved} glyphs")]
internal sealed class RedactionTally
{
    /// <summary>The codes removed, by font dictionary.</summary>
    private readonly Dictionary<PdfDictionary, HashSet<int>> _removedCodes = [];

    /// <summary>Gets or sets the number of pages changed.</summary>
    internal int Pages { get; set; }

    /// <summary>Gets or sets the number of areas applied.</summary>
    internal int Regions { get; set; }

    /// <summary>Gets or sets the number of glyphs removed.</summary>
    internal int GlyphsRemoved { get; set; }

    /// <summary>Gets or sets the number of images removed.</summary>
    internal int ImagesRemoved { get; set; }

    /// <summary>Gets or sets the number of images blanked.</summary>
    internal int ImagesBlanked { get; set; }

    /// <summary>Gets or sets the number of paths removed.</summary>
    internal int PathsRemoved { get; set; }

    /// <summary>Gets or sets the number of annotations removed.</summary>
    internal int AnnotationsRemoved { get; set; }

    /// <summary>Gets or sets the number of forms rewritten.</summary>
    internal int FormsRewritten { get; set; }

    /// <summary>Gets or sets the number of resources dropped.</summary>
    internal int ResourcesRemoved { get; set; }

    /// <summary>Gets or sets the number of /ToUnicode entries removed.</summary>
    internal int ToUnicodeEntriesRemoved { get; set; }

    /// <summary>Gets the fonts that lost codes, with the codes.</summary>
    internal Dictionary<PdfDictionary, HashSet<int>> RemovedCodes => _removedCodes;

    /// <summary>Notes that a character code of a font was removed.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <param name="code">The code.</param>
    internal void AddRemovedCode(PdfDictionary font, int code)
    {
        ref var codes = ref CollectionsMarshal.GetValueRefOrAddDefault(_removedCodes, font, out _);
        codes ??= [];
        _ = codes.Add(code);
    }

    /// <summary>Makes the public report.</summary>
    /// <returns>The report.</returns>
    internal PdfRedactionReport ToReport() =>
        new(Pages, Regions, GlyphsRemoved, ImagesRemoved, ImagesBlanked, PathsRemoved, AnnotationsRemoved, FormsRewritten, ResourcesRemoved, ToUnicodeEntriesRemoved);
}
