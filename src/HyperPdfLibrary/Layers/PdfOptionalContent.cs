// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Layers;

/// <summary>
/// A document's layers and which are shown. Changing visibility only changes how pages are drawn; the file is not
/// touched. The visibility set is replaced, never changed in place, so drawing threads read it without a lock, and
/// <see cref="Version"/> tells renderers when to redraw.
/// </summary>
[DebuggerDisplay("PdfOptionalContent: {Layers.Count} layers")]
public sealed class PdfOptionalContent
{
    /// <summary>The deepest visibility expression evaluated.</summary>
    private const int MaxExpressionDepth = 16;

    /// <summary>The objects references resolve against.</summary>
    private readonly PdfObjectStore _objects;

    /// <summary>Guards writers of <see cref="_hidden"/>.</summary>
    private readonly Lock _gate = new();

    /// <summary>The layers in document order, with their default visibility.</summary>
    private readonly PdfLayer[] _defaults;

    /// <summary>The object numbers of groups that are not for viewing, which are always shown.</summary>
    private readonly HashSet<int> _nonView;

    /// <summary>The sets of groups of which only one is shown.</summary>
    private readonly int[][] _radioGroups;

    /// <summary>The object numbers of the groups hidden when printing, which the layer switches do not change.</summary>
    private readonly HashSet<int> _printHidden;

    /// <summary>The object numbers of the hidden groups.</summary>
    private HashSet<int> _hidden;

    /// <summary>The visibility version.</summary>
    private int _version;

    /// <summary>Initializes a new instance of the <see cref="PdfOptionalContent"/> class.</summary>
    /// <param name="objects">The document's objects.</param>
    public PdfOptionalContent(PdfObjectStore objects)
    {
        ArgumentNullException.ThrowIfNull(objects);
        _objects = objects;
        var set = OptionalContentReader.Read(objects);
        _defaults = set.Layers;
        _hidden = set.Hidden;
        _nonView = set.NonView;
        _radioGroups = set.RadioGroups;
        _printHidden = set.PrintHidden;
    }

    /// <summary>Gets a value indicating whether the document has layers.</summary>
    public bool HasLayers => _defaults.Length > 0;

    /// <summary>Gets a number that changes whenever a layer is shown or hidden.</summary>
    public int Version => Volatile.Read(ref _version);

    /// <summary>Gets the layers in document order, with their current visibility.</summary>
    public IReadOnlyList<PdfLayer> Layers
    {
        get
        {
            var hidden = Volatile.Read(ref _hidden);
            var layers = new PdfLayer[_defaults.Length];
            for (var i = 0; i < layers.Length; i++)
            {
                layers[i] = _defaults[i] with { IsVisible = !hidden.Contains(_defaults[i].Id) };
            }

            return layers;
        }
    }

    /// <summary>Shows or hides a layer. Showing a layer hides the other layers of its radio button groups.</summary>
    /// <param name="id">The layer's object number.</param>
    /// <param name="visible">Whether to show it.</param>
    /// <returns><see langword="true"/> when the document has the layer.</returns>
    public bool SetVisible(int id, bool visible)
    {
        if (!HasLayer(id))
        {
            return false;
        }

        lock (_gate)
        {
            var next = new HashSet<int>(_hidden);
            var changed = visible ? next.Remove(id) : next.Add(id);
            if (visible)
            {
                changed |= HideSiblings(id, next);
            }

            if (changed)
            {
                Volatile.Write(ref _hidden, next);
                _ = Interlocked.Increment(ref _version);
            }
        }

        return true;
    }

    /// <summary>Determines whether content marked with an optional content group or membership dictionary is shown.</summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <returns><see langword="true"/> when the content is shown.</returns>
    public bool IsVisible(PdfValue value) => _defaults.Length == 0 || Evaluate(value, Volatile.Read(ref _hidden), 0);

    /// <summary>
    /// Determines whether content marked with an optional content group or membership dictionary prints, using each
    /// group's print usage (/Usage /Print /PrintState) and the configuration's Print event, as PDFium does when printing.
    /// </summary>
    /// <param name="value">The /OC value: a reference to an OCG or OCMD.</param>
    /// <returns><see langword="true"/> when the content prints.</returns>
    public bool IsPrinted(PdfValue value) => _defaults.Length == 0 || Evaluate(value, _printHidden, 0);

    /// <summary>Tells renderers to redraw every page, after an edit changed what a page paints.</summary>
    internal void InvalidatePages() => _ = Interlocked.Increment(ref _version);

    /// <summary>Sets the first version, before the layer set is shared, so it starts above an earlier set's versions.</summary>
    /// <param name="version">The first version.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void StartAt(int version) => Volatile.Write(ref _version, version);

    /// <summary>
    /// Determines whether a dictionary is a membership dictionary. A dictionary without a /Type is a group, as in PDFium,
    /// unless it carries /OCGs or /VE.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns><see langword="true"/> for an OCMD.</returns>
    private static bool IsMembership(PdfDictionary dictionary)
    {
        if (dictionary.IsName(KnownName.Type, KnownName.OCG))
        {
            return false;
        }

        return dictionary.IsName(KnownName.Type, KnownName.OCMD) || dictionary.ContainsKey(KnownName.OCGs) || dictionary.ContainsKey(KnownName.VE);
    }

    /// <summary>Determines whether the document lists a layer.</summary>
    /// <param name="id">The layer's object number.</param>
    /// <returns><see langword="true"/> when listed.</returns>
    private bool HasLayer(int id)
    {
        foreach (var layer in _defaults)
        {
            if (layer.Id == id)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Hides the other groups of every radio button set that holds a group.</summary>
    /// <param name="id">The group just shown.</param>
    /// <param name="hidden">The hidden set being built.</param>
    /// <returns><see langword="true"/> when a group was newly hidden.</returns>
    private bool HideSiblings(int id, HashSet<int> hidden)
    {
        var changed = false;
        foreach (var set in _radioGroups)
        {
            if (Array.IndexOf(set, id) < 0)
            {
                continue;
            }

            foreach (var sibling in set)
            {
                changed |= sibling != id && HasLayer(sibling) && hidden.Add(sibling);
            }
        }

        return changed;
    }

    /// <summary>Evaluates an OCG reference, an OCMD or a visibility expression.</summary>
    /// <param name="value">The value.</param>
    /// <param name="hidden">The hidden groups.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool Evaluate(PdfValue value, HashSet<int> hidden, int depth)
    {
        if (depth > MaxExpressionDepth)
        {
            return true;
        }

        var dictionary = _objects.Resolve(value).AsDictionary();
        if (dictionary is null)
        {
            return true;
        }

        if (!IsMembership(dictionary))
        {
            return IsGroupShown(value, hidden);
        }

        var expression = dictionary.GetArray(KnownName.VE);
        return expression is not null ? EvaluateExpression(expression, hidden, depth + 1) : EvaluatePolicy(dictionary, hidden);
    }

    /// <summary>Determines whether one group is shown.</summary>
    /// <param name="group">The group reference.</param>
    /// <param name="hidden">The hidden groups.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool IsGroupShown(PdfValue group, HashSet<int> hidden)
    {
        var id = group.AsReference();
        return !id.IsValid || !hidden.Contains(id.Number) || _nonView.Contains(id.Number);
    }

    /// <summary>Applies an OCMD's /P policy to its /OCGs, counting only the groups that resolve.</summary>
    /// <param name="membership">The OCMD.</param>
    /// <param name="hidden">The hidden groups.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluatePolicy(PdfDictionary membership, HashSet<int> hidden)
    {
        var groups = membership.GetRaw(KnownName.OCGs);
        var resolved = _objects.Resolve(groups);
        if (resolved.AsDictionary() is not null)
        {
            return IsGroupShown(groups, hidden);
        }

        if (resolved.AsArray() is not { } array)
        {
            return true;
        }

        var count = 0;
        var shown = 0;
        for (var i = 0; i < array.Count; i++)
        {
            var item = array.GetRaw(i);
            if (_objects.Resolve(item).AsDictionary() is null)
            {
                continue;
            }

            count++;
            shown += IsGroupShown(item, hidden) ? 1 : 0;
        }

        return count == 0 || membership.GetName(KnownName.P).ToKnownName() switch
        {
            KnownName.AllOn => shown == count,
            KnownName.AnyOff => shown < count,
            KnownName.AllOff => shown == 0,
            _ => shown > 0,
        };
    }

    /// <summary>Evaluates a /VE visibility expression: [/And ...], [/Or ...] or [/Not x].</summary>
    /// <param name="expression">The expression.</param>
    /// <param name="hidden">The hidden groups.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluateExpression(PdfArray expression, HashSet<int> hidden, int depth)
    {
        var op = expression.GetName(0).ToKnownName();
        if (op == KnownName.Not)
        {
            return !EvaluateOperand(expression.GetRaw(1), hidden, depth);
        }

        var isAnd = op == KnownName.And;
        for (var i = 1; i < expression.Count; i++)
        {
            var result = EvaluateOperand(expression.GetRaw(i), hidden, depth);
            if (result != isAnd)
            {
                return result;
            }
        }

        return isAnd;
    }

    /// <summary>Evaluates one operand of an expression: a group or a nested expression.</summary>
    /// <param name="operand">The operand.</param>
    /// <param name="hidden">The hidden groups.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when shown.</returns>
    private bool EvaluateOperand(PdfValue operand, HashSet<int> hidden, int depth) =>
        _objects.Resolve(operand).AsArray() is { } nested && depth <= MaxExpressionDepth
            ? EvaluateExpression(nested, hidden, depth + 1)
            : Evaluate(operand, hidden, depth);
}
