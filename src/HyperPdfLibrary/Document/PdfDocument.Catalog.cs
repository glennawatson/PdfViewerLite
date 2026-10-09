// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Features;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <content>Threads, output intents, permissions and piece info.</content>
public sealed partial class PdfDocument
{
    /// <summary>The most beads read in one thread.</summary>
    private const int MaxBeads = 100_000;

    /// <summary>The change level of a certification signature that names none.</summary>
    private const int DefaultDocMdpLevel = 2;

    /// <summary>Gets the article threads (the catalog's <c>/Threads</c>).</summary>
    /// <returns>The threads.</returns>
    public PdfThread[] GetThreads()
    {
        var list = Catalog.Array("Threads");
        var threads = new List<PdfThread>();
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is { } thread)
            {
                threads.Add(new(thread.Dict("I")?.GetText(KnownName.Title), ReadBeads(thread.Dict("F")), thread));
            }
        }

        return [.. threads];
    }

    /// <summary>Gets the output intents (the catalog's <c>/OutputIntents</c>).</summary>
    /// <returns>The output intents.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfOutputIntent[] GetOutputIntents() => ReadOutputIntents(Catalog);

    /// <summary>Gets the output intents of a page (PDF 2.0).</summary>
    /// <param name="page">The page.</param>
    /// <returns>The output intents.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfOutputIntent[] GetOutputIntents(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return ReadOutputIntents(page.Dictionary);
    }

    /// <summary>Gets the permissions (<c>/Perms</c>) and the legal attestation (<c>/Legal</c>).</summary>
    /// <returns>The permissions, or <see langword="null"/> when the catalog has neither entry.</returns>
    public PdfPermissions? GetPermissions()
    {
        var perms = Catalog.Dict("Perms");
        var legal = Catalog.Dict("Legal");
        if (perms is null && legal is null)
        {
            return null;
        }

        var counts = new Dictionary<string, long>(StringComparer.Ordinal);
        for (var i = 0; legal is not null && i < legal.Count; i++)
        {
            if (Objects.Resolve(legal.GetValueAt(i)) is { IsNumber: true } count)
            {
                counts[Objects.Names.GetString(legal.GetKeyAt(i))] = count.AsInteger();
            }
        }

        var certification = perms?.Dict("DocMDP");
        return new(certification is not null, certification is null ? null : ReadDocMdpLevel(certification), perms?.Dict("UR3") is not null, counts, legal?.Text("Attestation"));
    }

    /// <summary>Gets the page-piece data of the catalog (<c>/PieceInfo</c>).</summary>
    /// <returns>One entry per application.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public PdfPieceData[] GetPieceInfo() => GetPieceInfo(Catalog);

    /// <summary>Gets the page-piece data of a page.</summary>
    /// <param name="page">The page.</param>
    /// <returns>One entry per application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public PdfPieceData[] GetPieceInfo(PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        return GetPieceInfo(page.Dictionary);
    }

    /// <summary>Gets the page-piece data of any dictionary that has <c>/PieceInfo</c> (a page, a form XObject or the catalog).</summary>
    /// <param name="owner">The dictionary.</param>
    /// <returns>One entry per application.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="owner"/> is null.</exception>
    public PdfPieceData[] GetPieceInfo(PdfDictionary owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        if (owner.Dict("PieceInfo") is not { } info)
        {
            return [];
        }

        var pieces = new List<PdfPieceData>(info.Count);
        for (var i = 0; i < info.Count; i++)
        {
            var data = Objects.Resolve(info.GetValueAt(i)).AsDictionary();
            pieces.Add(new(Objects.Names.GetString(info.GetKeyAt(i)), data is null ? null : PdfDate.Parse(data.Value("LastModified").AsStringBytes()), data?.Dict("Private")));
        }

        return [.. pieces];
    }

    /// <summary>Reads output intents from a catalog or page.</summary>
    /// <param name="owner">The dictionary holding <c>/OutputIntents</c>.</param>
    /// <returns>The output intents.</returns>
    private static PdfOutputIntent[] ReadOutputIntents(PdfDictionary owner)
    {
        var list = owner.Array("OutputIntents");
        var intents = new List<PdfOutputIntent>();
        for (var i = 0; list is not null && i < list.Count; i++)
        {
            if (list.GetDictionary(i) is not { } intent)
            {
                continue;
            }

            var profile = intent.Stream("DestOutputProfile");
            intents.Add(new(
                intent.NameText("S") ?? string.Empty,
                intent.Text("OutputCondition"),
                intent.Text("OutputConditionIdentifier"),
                intent.Text("RegistryName"),
                intent.Text("Info"),
                profile?.Dictionary.Int("N", 0) ?? 0,
                profile));
        }

        return [.. intents];
    }

    /// <summary>Reads the change level of a certification signature from its transform parameters.</summary>
    /// <param name="signature">The <c>/DocMDP</c> signature dictionary.</param>
    /// <returns>The level, or null when the signature has no DocMDP reference.</returns>
    private static int? ReadDocMdpLevel(PdfDictionary signature)
    {
        var references = signature.Array("Reference");
        for (var i = 0; references is not null && i < references.Count; i++)
        {
            if (references.GetDictionary(i) is { } reference && reference.NameText("TransformMethod") == "DocMDP")
            {
                return reference.Dict("TransformParams")?.Int("P", DefaultDocMdpLevel) ?? DefaultDocMdpLevel;
            }
        }

        return null;
    }

    /// <summary>Reads the beads of a thread by following <c>/N</c> from the first bead until the ring closes.</summary>
    /// <param name="first">The first bead (<c>/F</c>), or null.</param>
    /// <returns>The beads.</returns>
    private PdfBead[] ReadBeads(PdfDictionary? first)
    {
        var beads = new List<PdfBead>();
        var visited = new HashSet<PdfDictionary>(ReferenceEqualityComparer.Instance);
        for (var bead = first; bead is not null && beads.Count < MaxBeads && visited.Add(bead); bead = bead.GetDictionary(KnownName.N))
        {
            var page = bead.GetRaw(KnownName.P);
            beads.Add(new(page.IsReference ? GetPageIndex(page.AsReference()) : -1, bead.Rect("R")));
        }

        return [.. beads];
    }
}
