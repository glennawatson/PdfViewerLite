// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>
/// Registers the optional content groups the copied pages use. Copying page content copies the groups it names, but only
/// <c>/OCProperties</c> makes them layers. Each copied group joins the target's <c>/OCGs</c> and its default
/// configuration, shown or hidden as the source showed it.
/// </summary>
[DebuggerDisplay("PdfCarryLayers")]
internal sealed class PdfCarryLayers
{
    /// <summary>The context.</summary>
    private readonly PdfCarryContext _context;

    /// <summary>Initializes a new instance of the <see cref="PdfCarryLayers"/> class.</summary>
    /// <param name="context">The context.</param>
    internal PdfCarryLayers(PdfCarryContext context) => _context = context;

    /// <summary>Adds the copied optional content groups to the target's optional content properties.</summary>
    internal void Finish()
    {
        var groups = FindCopiedGroups();
        if (groups.Count == 0)
        {
            return;
        }

        var catalog = _context.Catalog;
        var properties = catalog.GetDictionary(KnownName.OCProperties)?.Clone() ?? _context.NewDictionary();
        properties.Set(KnownName.OCGs, PdfValue.FromArray(ListGroups(properties.GetArray(KnownName.OCGs), groups)));
        ConfigureChosen(properties, groups);
        properties.Set(KnownName.D, PdfValue.FromDictionary(Configure(properties.GetDictionary(KnownName.D), groups)));
        _context.SetEntry(catalog, KnownName.OCProperties, properties);
        _context.CatalogChanged = true;
    }

    /// <summary>Sets an array unless there is none or it is empty.</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="key">The key.</param>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    private static void SetIfAny(PdfDictionary dictionary, KnownName key, PdfArray? array)
    {
        if (array is { Count: > 0 })
        {
            dictionary.Set(key, PdfValue.FromArray(array));
        }
    }

    /// <summary>Lists the copied groups after the groups the target already lists.</summary>
    /// <param name="listed">The target's /OCGs array, or <see langword="null"/>.</param>
    /// <param name="groups">The copied groups.</param>
    /// <returns>The new array.</returns>
    private PdfArray ListGroups(PdfArray? listed, List<CopiedGroup> groups)
    {
        var references = listed?.Clone() ?? new PdfArray(_context.TargetStore);
        foreach (var group in groups)
        {
            references.Add(Reference(group));
        }

        return references;
    }

    /// <summary>Gets a reference to the copy of a group.</summary>
    /// <param name="group">The group.</param>
    /// <returns>The reference.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PdfValue Reference(CopiedGroup group) => PdfValue.FromReference(new(_context.Sink.GetMapped(group.Number), 0));

    /// <summary>Finds the optional content groups among the objects copied, with whether the source hides each.</summary>
    /// <returns>The groups.</returns>
    private List<CopiedGroup> FindCopiedGroups()
    {
        var source = _context.Source;
        var groups = new List<CopiedGroup>();
        HashSet<int>? hidden = null;
        for (var number = 1; number < source.Size; number++)
        {
            if (!IsCopiedGroup(number))
            {
                continue;
            }

            hidden ??= OptionalContentReader.Read(source).Hidden;
            groups.Add(new(number, hidden.Contains(number)));
        }

        return groups;
    }

    /// <summary>Determines whether a source object is an optional content group that was copied.</summary>
    /// <param name="number">The source object number.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private bool IsCopiedGroup(int number) =>
        _context.Sink.GetMapped(number) > 0
        && StoreReading.GetDictionary(
        _context.Source,
        new(
        number,
        0)) is { } group
        && group.IsName(
        KnownName.Type,
        KnownName.OCG);

    /// <summary>Makes a configuration list each group in the state the source showed it.</summary>
    /// <param name="configuration">The target's configuration, or <see langword="null"/>.</param>
    /// <param name="groups">The copied groups.</param>
    /// <returns>The changed copy.</returns>
    private PdfDictionary Configure(PdfDictionary? configuration, List<CopiedGroup> groups)
    {
        var copy = configuration?.Clone() ?? _context.NewDictionary();
        var baseHidden = copy.IsName(KnownName.BaseState, KnownName.OFF);
        var on = Extend(copy.GetArray(KnownName.ON));
        var off = Extend(copy.GetArray(KnownName.OFF));
        var order = copy.GetArray(KnownName.Order)?.Clone();
        foreach (var group in groups)
        {
            var reference = Reference(group);
            order?.Add(reference);
            if (group.IsHidden != baseHidden)
            {
                (group.IsHidden ? off : on).Add(reference);
            }
        }

        SetIfAny(copy, KnownName.ON, on);
        SetIfAny(copy, KnownName.OFF, off);
        SetIfAny(copy, KnownName.Order, order);
        return copy;
    }

    /// <summary>Copies an array so groups can be added, or creates an empty one.</summary>
    /// <param name="array">The target's array, or <see langword="null"/>.</param>
    /// <returns>The array to add to.</returns>
    private PdfArray Extend(PdfArray? array) => array?.Clone() ?? new PdfArray(_context.TargetStore);

    /// <summary>Applies the same change to the configuration that readers use when it is not the default one.</summary>
    /// <param name="properties">The optional content properties, changed in place.</param>
    /// <param name="groups">The copied groups.</param>
    private void ConfigureChosen(PdfDictionary properties, List<CopiedGroup> groups)
    {
        var chosen = OptionalContentReader.PickConfiguration(properties);
        var configs = properties.GetArray(KnownName.Configs);
        if (chosen is null || configs is null)
        {
            return;
        }

        var changed = configs.Clone();
        for (var i = 0; i < changed.Count; i++)
        {
            if (ReferenceEquals(changed.GetDictionary(i), chosen))
            {
                changed.SetAt(i, PdfValue.FromDictionary(Configure(chosen, groups)));
            }
        }

        properties.Set(KnownName.Configs, PdfValue.FromArray(changed));
    }

    /// <summary>An optional content group that was copied.</summary>
    /// <param name="Number">The group's object number in the source.</param>
    /// <param name="IsHidden">Whether the source's default configuration hides it.</param>
    [DebuggerDisplay("CopiedGroup: {Number}")]
    private readonly record struct CopiedGroup(int Number, bool IsHidden);
}
