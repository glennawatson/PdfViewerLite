// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// One pass over a document's objects and page resources that counts the features a conformance reading report lists. It
/// reads each object once, follows no references, and does not decode content unless the file has no output intent.
/// </summary>
[DebuggerDisplay("ConformanceScan: {_lzw} lzw, {_scripts} scripts, {_fonts.Count} fonts")]
internal sealed partial class ConformanceScan
{
    /// <summary>The most font names listed in an observation's detail.</summary>
    private const int MaxListedFonts = 8;

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The names of fonts without an embedded program.</summary>
    private readonly List<string> _fonts = [];

    /// <summary>The dictionaries already walked for resources, so shared resources are read once.</summary>
    private readonly HashSet<PdfDictionary> _visited = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The interned <c>/ca</c> name, which the known-name list does not hold.</summary>
    private readonly PdfName _lowerCa;

    /// <summary>The streams that use LZW.</summary>
    private int _lzw;

    /// <summary>The JavaScript actions and scripts.</summary>
    private int _scripts;

    /// <summary>The soft masks.</summary>
    private int _softMasks;

    /// <summary>The constant alpha values below 1.</summary>
    private int _alphas;

    /// <summary>The blend modes other than Normal.</summary>
    private int _blendModes;

    /// <summary>The transparency groups.</summary>
    private int _groups;

    /// <summary>The streams that refer to external content.</summary>
    private int _external;

    /// <summary>Whether a page uses device colour.</summary>
    private bool _usesDeviceColor;

    /// <summary>Initializes a new instance of the <see cref="ConformanceScan"/> class.</summary>
    /// <param name="document">The document.</param>
    internal ConformanceScan(PdfDocument document)
    {
        _document = document;
        _lowerCa = document.Objects.Names.Intern("ca");
    }

    /// <summary>Runs the scan and lists what it saw.</summary>
    /// <param name="claim">The PDF/A claim, or null.</param>
    /// <param name="hasOutputIntent">Whether the catalog has an output intent.</param>
    /// <returns>The observations in finding order.</returns>
    internal PdfConformanceObservation[] Run(PdfAClaim? claim, bool hasOutputIntent)
    {
        ScanObjects();
        ScanPages(!hasOutputIntent);
        var observations = new List<PdfConformanceObservation>();
        AddIf(observations, PdfConformanceFinding.Encryption, _document.IsEncrypted ? 1 : 0, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.JavaScript, _scripts, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.LzwFilter, _lzw, null, claim is { ForbidsLzw: true });
        AddIf(observations, PdfConformanceFinding.NonEmbeddedFont, _fonts.Count, FontDetail(), claim is not null);
        var transparent = _softMasks + _alphas + _blendModes + _groups;
        AddIf(observations, PdfConformanceFinding.Transparency, transparent, TransparencyDetail(), claim is { ForbidsTransparency: true });
        var files = CountEmbeddedFiles();
        AddIf(observations, PdfConformanceFinding.EmbeddedFiles, files, null, claim is { ForbidsEmbeddedFiles: true });
        AddIf(observations, PdfConformanceFinding.ExternalContent, _external, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.MissingOutputIntent, _usesDeviceColor ? 1 : 0, null, claim is not null);
        return [.. observations];
    }

    /// <summary>Adds an observation when the count is above zero.</summary>
    /// <param name="observations">The list to add to.</param>
    /// <param name="kind">The finding.</param>
    /// <param name="count">How many were seen.</param>
    /// <param name="detail">The detail text, or null.</param>
    /// <param name="forbidden">Whether the claimed part forbids the feature.</param>
    private static void AddIf(List<PdfConformanceObservation> observations, PdfConformanceFinding kind, int count, string? detail, bool forbidden)
    {
        if (count > 0)
        {
            observations.Add(new(kind, count, detail, forbidden));
        }
    }

    /// <summary>Counts the embedded files: the name tree entries, or the <c>/AF</c> entries when there are more.</summary>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CountEmbeddedFiles() =>
        Math.Max(_document.GetAttachments().Count, _document.Catalog.GetArray(_document.Objects.Names.Intern("AF"))?.Count ?? 0);

    /// <summary>Joins the listed font names.</summary>
    /// <returns>The names, with a count of the rest when there are more than the limit.</returns>
    private string? FontDetail()
    {
        if (_fonts.Count == 0)
        {
            return null;
        }

        var listed = string.Join(", ", _fonts.GetRange(0, Math.Min(_fonts.Count, MaxListedFonts)));
        return _fonts.Count > MaxListedFonts ? string.Create(CultureInfo.InvariantCulture, $"{listed} and {_fonts.Count - MaxListedFonts} more") : listed;
    }

    /// <summary>Describes which transparency features were seen.</summary>
    /// <returns>The text, or null when none were.</returns>
    private string? TransparencyDetail() =>
        _softMasks + _alphas + _blendModes + _groups == 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"soft masks {_softMasks}, alpha below 1 {_alphas}, blend modes {_blendModes}, groups {_groups}");
}
