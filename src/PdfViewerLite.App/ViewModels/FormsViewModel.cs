// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Form filling for one tab. Text fields open an editor placed over the field; check boxes and radio buttons toggle on
/// click; choices are picked from a menu. Every change goes to the document straight away.
/// </summary>
[DebuggerDisplay("FormsViewModel: Editing {Editing}")]
public sealed partial class FormsViewModel : ReactiveObject
{
    /// <summary>The most passes of recalculation, so fields that depend on each other in a loop still settle.</summary>
    private const int MaxCalculationPasses = 8;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Fields read for hit testing, reused.</summary>
    private readonly List<FormField> _scratch = [];

    /// <summary>The fields' scripts by name, read on first use.</summary>
    private Dictionary<string, FieldScripts>? _scripts;

    /// <summary>The fields with a calculation, in page order, read on first use.</summary>
    private List<FieldScripts>? _calculated;

    /// <summary>Initializes a new instance of the <see cref="FormsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public FormsViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets a value indicating whether the document has a fillable form.</summary>
    public bool HasForm => Filler?.HasForm == true;

    /// <summary>Gets the text field being edited, or <see langword="null"/>.</summary>
    [Reactive]
    public partial FormField? Editing { get; private set; }

    /// <summary>Gets or sets the text in the open editor.</summary>
    [Reactive]
    public partial string EditText { get; set; } = string.Empty;

    /// <summary>Gets the document's form filler, or <see langword="null"/>.</summary>
    private IFormFiller? Filler => _owner.TryGetDocument() as IFormFiller;

    /// <summary>Finds the fillable field under a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The field, or <see langword="null"/>.</returns>
    public FormField? HitTest(int page, PagePoint point)
    {
        if (Filler is not { HasForm: true } filler)
        {
            return null;
        }

        _scratch.Clear();
        filler.GetFields(page, _scratch);
        foreach (var field in _scratch)
        {
            if (!field.IsReadOnly && field.Kind != FormFieldKind.Unknown && field.Bounds.Contains(point))
            {
                return field;
            }
        }

        return null;
    }

    /// <summary>Acts on a click on a field: edit text, toggle a box or radio button. Choices are handled by the view's menu.</summary>
    /// <param name="field">The field.</param>
    /// <returns><see langword="true"/> when handled here.</returns>
    public bool Activate(FormField field)
    {
        ArgumentNullException.ThrowIfNull(field);
        switch (field.Kind)
        {
            case FormFieldKind.Text:
            {
                Commit();
                EditText = field.Value;
                Editing = field;
                return true;
            }

            case FormFieldKind.CheckBox:
            {
                Refresh(field, Filler?.SetChecked(field.PageIndex, field.Index, !field.IsChecked) == true);
                Recalculate();
                return true;
            }

            case FormFieldKind.RadioButton:
            {
                Refresh(field, Filler?.SetChecked(field.PageIndex, field.Index, true) == true);
                Recalculate();
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Picks a choice of a combo or list box.</summary>
    /// <param name="field">The field.</param>
    /// <param name="option">The option index.</param>
    public void Choose(FormField field, int option)
    {
        ArgumentNullException.ThrowIfNull(field);
        Refresh(field, Filler?.SelectOption(field.PageIndex, field.Index, option) == true);
        Recalculate();
    }

    /// <summary>Writes the open editor's text into its field and closes the editor.</summary>
    public void Commit()
    {
        if (Editing is not { } field)
        {
            return;
        }

        var scripts = ScriptsFor(field.Name);
        if (scripts is not null && Problem(scripts, EditText) is { } problem)
        {
            // Keep the editor open so the value can be corrected; the message stays until dismissed.
            _owner.Notice = problem;
            return;
        }

        Editing = null;
        var text = scripts is null ? EditText : FormScriptEngine.Format(scripts.Format, EditText);
        if (string.Equals(field.Value, text, StringComparison.Ordinal))
        {
            return;
        }

        Refresh(field, Filler?.SetText(field.PageIndex, field.Index, text) == true);
        Recalculate();
    }

    /// <summary>Closes the editor without changing the field.</summary>
    public void Cancel() => Editing = null;

    /// <summary>Commits the editor and opens the next text field, in page order, as Tab does.</summary>
    /// <returns>The next field, or <see langword="null"/> when there is none.</returns>
    public FormField? CommitAndMoveNext()
    {
        var current = Editing;
        Commit();
        if (Editing is { } refused)
        {
            // The value was refused; stay on the field so it can be corrected.
            return refused;
        }

        if (current is null || Filler is not { } filler)
        {
            return null;
        }

        for (var page = current.PageIndex; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            filler.GetFields(page, _scratch);
            foreach (var field in _scratch)
            {
                if (field.Kind != FormFieldKind.Text || field.IsReadOnly || (page == current.PageIndex && field.Index <= current.Index))
                {
                    continue;
                }

                _ = Activate(field);
                return field;
            }
        }

        return null;
    }

    /// <summary>
    /// Recalculates the calculated fields from the others, as the form's calculate scripts ask, repeating until
    /// nothing changes so a total of totals settles. Results are formatted as each field's format script asks.
    /// </summary>
    public void Recalculate()
    {
        if (Filler is not { } filler || LoadScripts() is not { Count: > 0 } calculated)
        {
            return;
        }

        for (var pass = 0; pass < MaxCalculationPasses; pass++)
        {
            var values = ReadValues(filler);
            var changed = false;
            foreach (var scripts in calculated)
            {
                if (!FormScriptEngine.TryCalculate(scripts.Calculate, name => values.GetValueOrDefault(name), out var result))
                {
                    continue;
                }

                var text = FormScriptEngine.Format(scripts.Format, FormNumbers.Plain(result));
                if (string.Equals(values.GetValueOrDefault(scripts.Name), text, StringComparison.Ordinal) || !filler.SetText(scripts.PageIndex, scripts.Index, text))
                {
                    continue;
                }

                values[scripts.Name] = text;
                changed = true;
                _owner.OnPageEdited(scripts.PageIndex);
            }

            if (!changed)
            {
                return;
            }
        }
    }

    /// <summary>Explains why a typed value is refused by its field's keystroke or validate script.</summary>
    /// <param name="scripts">The field's scripts.</param>
    /// <param name="text">The typed value.</param>
    /// <returns>The explanation, or <see langword="null"/> when the value is accepted.</returns>
    private static string? Problem(FieldScripts scripts, string text)
    {
        if (!FormScriptEngine.Accepts(scripts.Keystroke, text))
        {
            return scripts.Keystroke.Function switch
            {
                FormScriptFunction.Date => $"Type a date like {FormScriptEngine.Format(scripts.Keystroke, "2026-03-07")}.",
                FormScriptFunction.Special => "Type the number with the right count of digits.",
                _ => "Type a number.",
            };
        }

        return FormScriptEngine.Validate(scripts.Validate, text, out var message) ? null : message;
    }

    /// <summary>Picks a choice of a combo or list box.</summary>
    /// <param name="choice">The field and option.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void ChooseOption(FormChoice choice) => Choose(choice.Field, choice.Option);

    /// <summary>Reads every field's value by name.</summary>
    /// <param name="filler">The form.</param>
    /// <returns>The values.</returns>
    private Dictionary<string, string> ReadValues(IFormFiller filler)
    {
        var values = new Dictionary<string, string>(StringComparer.Ordinal);
        for (var page = 0; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            filler.GetFields(page, _scratch);
            foreach (var field in _scratch)
            {
                _ = values.TryAdd(field.Name, field.Kind is FormFieldKind.CheckBox or FormFieldKind.RadioButton && !field.IsChecked ? string.Empty : field.Value);
            }
        }

        return values;
    }

    /// <summary>Gets a field's scripts by name.</summary>
    /// <param name="name">The field's name.</param>
    /// <returns>The scripts, or <see langword="null"/> when it has none PdfViewerLite runs.</returns>
    private FieldScripts? ScriptsFor(string name)
    {
        _ = LoadScripts();
        return _scripts?.GetValueOrDefault(name);
    }

    /// <summary>Reads the form's scripts once, returning the calculated fields.</summary>
    /// <returns>The calculated fields, or <see langword="null"/> when the document has no scripts.</returns>
    private List<FieldScripts>? LoadScripts()
    {
        if (_scripts is not null)
        {
            return _calculated;
        }

        _scripts = [with(StringComparer.Ordinal)];
        _calculated = [];
        if (_owner.TryGetDocument() is not IFormScriptSource source)
        {
            return _calculated;
        }

        var all = new List<FieldScripts>();
        for (var page = 0; page < _owner.PageCount; page++)
        {
            source.GetScripts(page, all);
        }

        foreach (var scripts in all)
        {
            _ = _scripts.TryAdd(scripts.Name, scripts);
            if (scripts.Calculate.Function != FormScriptFunction.Unknown)
            {
                _calculated.Add(scripts);
            }
        }

        return _calculated;
    }

    /// <summary>Redraws the page of a changed field.</summary>
    /// <param name="field">The field.</param>
    /// <param name="changed">Whether it changed.</param>
    private void Refresh(FormField field, bool changed)
    {
        if (changed)
        {
            _owner.OnPageEdited(field.PageIndex);
        }
    }
}
