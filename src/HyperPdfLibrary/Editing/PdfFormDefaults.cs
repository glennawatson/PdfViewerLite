// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using ByteArrayComparer = HyperPdfLibrary.Objects.ByteArrayComparer;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Works out how the source form's defaults meet the target's. A source font resource whose name the target uses for
/// another font gets a new name, which the carried fields' default appearance strings (<c>/DA</c>) follow. A default
/// appearance or quadding (<c>/Q</c>) that differs from the target's becomes explicit on each carried root field, so no
/// existing field changes and no carried field changes look.
/// </summary>
[DebuggerDisplay("PdfFormDefaults: {Renames.Count} renamed fonts")]
internal sealed class PdfFormDefaults
{
    /// <summary>The first byte of a name token.</summary>
    private const byte NameStart = (byte)'/';

    /// <summary>Initializes a new instance of the <see cref="PdfFormDefaults"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfFormDefaults(PdfCarryContext context)
    {
        SourceForm = context.Source.Catalog.GetDictionary(KnownName.AcroForm);
        var targetForm = context.Catalog.GetDictionary(KnownName.AcroForm);
        HasTargetForm = targetForm is not null;
        Renames = FindRenames(context, targetForm);
        var sourceAppearance = RenameFonts(GetAppearance(SourceForm));
        Appearance = Differs(sourceAppearance, GetAppearance(targetForm)) ? sourceAppearance : null;
        Quadding = FindQuadding(SourceForm, targetForm);
    }

    /// <summary>Gets the source AcroForm, or <see langword="null"/>.</summary>
    internal PdfDictionary? SourceForm { get; }

    /// <summary>Gets a value indicating whether the target already has an AcroForm.</summary>
    internal bool HasTargetForm { get; }

    /// <summary>Gets the new spelling of each source font resource name that clashed, by source spelling.</summary>
    internal Dictionary<byte[], byte[]> Renames { get; }

    /// <summary>Gets the default appearance carried root fields need, or <see langword="null"/> when they inherit the target's.</summary>
    internal byte[]? Appearance { get; }

    /// <summary>Gets the quadding carried root fields need, or <see langword="null"/> when they inherit the target's.</summary>
    internal int? Quadding { get; }

    /// <summary>Follows the font renames in a default appearance string.</summary>
    /// <param name="appearance">The string's bytes.</param>
    /// <returns>A copy of the bytes with the renamed fonts.</returns>
    internal byte[] RenameFonts(ReadOnlySpan<byte> appearance)
    {
        if (Renames.Count == 0 || !appearance.Contains(NameStart))
        {
            return appearance.ToArray();
        }

        var lookup = Renames.GetAlternateLookup<ReadOnlySpan<byte>>();
        var output = new ArrayBufferWriter<byte>(appearance.Length + Renames.Count);
        var start = 0;
        while (start < appearance.Length)
        {
            var end = start;
            while (end < appearance.Length && !IsSpace(appearance[end]))
            {
                end++;
            }

            WriteToken(output, appearance[start..end], lookup);
            start = end;
            while (start < appearance.Length && IsSpace(appearance[start]))
            {
                output.Write(appearance.Slice(start, 1));
                start++;
            }
        }

        return output.WrittenSpan.ToArray();
    }

    /// <summary>Gets a form's default appearance string.</summary>
    /// <param name="form">The AcroForm, or <see langword="null"/>.</param>
    /// <returns>The bytes; empty when there are none.</returns>
    private static ReadOnlySpan<byte> GetAppearance(PdfDictionary? form) => form is null ? [] : form.GetStringBytes(KnownName.DA);

    /// <summary>Determines whether the source has a default appearance the target's form does not give.</summary>
    /// <param name="source">The source's default appearance, with fonts renamed.</param>
    /// <param name="target">The target's default appearance.</param>
    /// <returns><see langword="true"/> when the source has one and the target's differs.</returns>
    private static bool Differs(byte[] source, ReadOnlySpan<byte> target) => source.Length > 0 && !target.SequenceEqual(source);

    /// <summary>Gets the quadding carried root fields need.</summary>
    /// <param name="source">The source AcroForm, or <see langword="null"/>.</param>
    /// <param name="target">The target AcroForm, or <see langword="null"/>.</param>
    /// <returns>The source's quadding when it differs from the target's, otherwise <see langword="null"/>.</returns>
    private static int? FindQuadding(PdfDictionary? source, PdfDictionary? target)
    {
        var quadding = source?.GetInt32(KnownName.Q) ?? 0;
        return quadding == (target?.GetInt32(KnownName.Q) ?? 0) ? null : quadding;
    }

    /// <summary>Writes a token, replacing a renamed font name.</summary>
    /// <param name="output">The output.</param>
    /// <param name="token">The token.</param>
    /// <param name="renames">The renames, looked up by spelling.</param>
    private static void WriteToken(ArrayBufferWriter<byte> output, ReadOnlySpan<byte> token, Dictionary<byte[], byte[]>.AlternateLookup<ReadOnlySpan<byte>> renames)
    {
        if (token.Length > 1 && token[0] == NameStart && renames.TryGetValue(token[1..], out var replacement))
        {
            output.Write("/"u8);
            output.Write(replacement);
            return;
        }

        output.Write(token);
    }

    /// <summary>Determines whether a list of spellings holds one.</summary>
    /// <param name="chosen">The renames chosen so far.</param>
    /// <param name="candidate">The spelling.</param>
    /// <returns><see langword="true"/> when a rename already uses it.</returns>
    private static bool IsChosen(Dictionary<byte[], byte[]> chosen, byte[] candidate)
    {
        foreach (var spelling in chosen.Values)
        {
            if (spelling.AsSpan().SequenceEqual(candidate))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a byte separates tokens.</summary>
    /// <param name="value">The byte.</param>
    /// <returns><see langword="true"/> for white space.</returns>
    private static bool IsSpace(byte value) => value is (byte)' ' or (byte)'\n' or (byte)'\r' or (byte)'\t' or (byte)'\f' or 0;

    /// <summary>Determines whether two font dictionaries describe the same font.</summary>
    /// <param name="a">The first font.</param>
    /// <param name="namesA">The first font's name table.</param>
    /// <param name="b">The second font.</param>
    /// <param name="namesB">The second font's name table.</param>
    /// <returns><see langword="true"/> when their type and base font agree.</returns>
    private static bool SameFont(PdfDictionary a, PdfNameTable namesA, PdfDictionary b, PdfNameTable namesB) =>
        namesA.GetSpelling(a.GetName(KnownName.Subtype)).SequenceEqual(namesB.GetSpelling(b.GetName(KnownName.Subtype)))
        && namesA.GetSpelling(a.GetName(KnownName.BaseFont)).SequenceEqual(namesB.GetSpelling(b.GetName(KnownName.BaseFont)))
        && namesA.GetSpelling(a.GetName(KnownName.Encoding)).SequenceEqual(namesB.GetSpelling(b.GetName(KnownName.Encoding)));

    /// <summary>Determines whether a source font resource and a target font resource are the same font.</summary>
    /// <param name="context">The context.</param>
    /// <param name="sourceRaw">The source resource as stored.</param>
    /// <param name="targetFonts">The target font resources.</param>
    /// <param name="name">The name in the target.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool IsSameResource(PdfCarryContext context, PdfValue sourceRaw, PdfDictionary targetFonts, PdfName name)
    {
        var targetRaw = targetFonts.GetRaw(name);
        if (ReferenceEquals(context.Source, context.TargetStore) && sourceRaw.IsReference && sourceRaw.AsReference() == targetRaw.AsReference())
        {
            return true;
        }

        return context.Source.Resolve(sourceRaw).AsDictionary() is { } a && targetFonts.Get(name).AsDictionary() is { } b
            && SameFont(a, context.Source.Names, b, context.TargetStore!.Names);
    }

    /// <summary>Gets the font resources of a form's default resources.</summary>
    /// <param name="form">The AcroForm, or <see langword="null"/>.</param>
    /// <returns>The font resources, or <see langword="null"/>.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static PdfDictionary? GetFonts(PdfDictionary? form) => form?.GetDictionary(KnownName.DR)?.GetDictionary(KnownName.Font);

    /// <summary>Determines whether a source font resource has the name of a different font in the target.</summary>
    /// <param name="context">The context.</param>
    /// <param name="sourceRaw">The source resource as stored.</param>
    /// <param name="targetFonts">The target font resources.</param>
    /// <param name="name">The name in the target.</param>
    /// <returns><see langword="true"/> when the target uses the name for another font.</returns>
    private static bool IsClash(PdfCarryContext context, PdfValue sourceRaw, PdfDictionary targetFonts, PdfName name) =>
        targetFonts.ContainsKey(name) && !IsSameResource(context, sourceRaw, targetFonts, name);

    /// <summary>Picks a font resource name that neither the source nor the target uses.</summary>
    /// <param name="spelling">The clashing name.</param>
    /// <param name="used">The font resources of the source and the target.</param>
    /// <param name="chosen">The renames chosen so far.</param>
    /// <returns>The new spelling.</returns>
    private static byte[] FreeName(byte[] spelling, in FontTables used, Dictionary<byte[], byte[]> chosen)
    {
        var number = 1;
        var candidate = PdfCarryText.WithSuffix(spelling, number);
        while (used.Contain(candidate) || IsChosen(chosen, candidate))
        {
            number++;
            candidate = PdfCarryText.WithSuffix(spelling, number);
        }

        return candidate;
    }

    /// <summary>Finds the source font resource names that the target uses for other fonts.</summary>
    /// <param name="context">The context.</param>
    /// <param name="targetForm">The target's AcroForm, or <see langword="null"/>.</param>
    /// <returns>The new spelling of each, by source spelling.</returns>
    private Dictionary<byte[], byte[]> FindRenames(PdfCarryContext context, PdfDictionary? targetForm)
    {
        var renames = new Dictionary<byte[], byte[]>(ByteArrayComparer.Instance);
        var sourceFonts = GetFonts(SourceForm);
        var targetFonts = GetFonts(targetForm);
        if (sourceFonts is null || targetFonts is null || context.TargetStore is not { } targetStore)
        {
            return renames;
        }

        var tables = new FontTables(sourceFonts, context.Source.Names, targetFonts, targetStore.Names);
        for (var i = 0; i < sourceFonts.Count; i++)
        {
            var spelling = context.Source.Names.GetSpelling(sourceFonts.GetKeyAt(i)).ToArray();
            if (IsClash(context, sourceFonts.GetValueAt(i), targetFonts, targetStore.Names.Intern(spelling)))
            {
                renames[spelling] = FreeName(spelling, in tables, renames);
            }
        }

        return renames;
    }

    /// <summary>The font resources of the source and the target, for choosing a name neither uses.</summary>
    /// <param name="SourceFonts">The source font resources.</param>
    /// <param name="SourceNames">The source name table.</param>
    /// <param name="TargetFonts">The target font resources.</param>
    /// <param name="TargetNames">The target name table.</param>
    [DebuggerDisplay("FontTables")]
    private readonly record struct FontTables(PdfDictionary SourceFonts, PdfNameTable SourceNames, PdfDictionary TargetFonts, PdfNameTable TargetNames)
    {
        /// <summary>Determines whether either side has a font resource of a name.</summary>
        /// <param name="spelling">The name's spelling.</param>
        /// <returns><see langword="true"/> when one does.</returns>
        internal bool Contain(byte[] spelling) =>
            TargetFonts.ContainsKey(TargetNames.Intern(spelling)) || SourceFonts.ContainsKey(SourceNames.Intern(spelling));
    }
}
