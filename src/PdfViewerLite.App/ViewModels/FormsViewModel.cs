// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Form filling for one tab. Text fields open an editor placed over the field; check boxes and radio buttons toggle on
/// click; choices are picked from a menu. Every change goes to the document straight away.
/// </summary>
[DebuggerDisplay("Editing {Editing}")]
public sealed class FormsViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Fields read for hit testing, reused.</summary>
    private readonly List<FormField> _scratch = [];

    /// <summary>Initializes a new instance of the <see cref="FormsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public FormsViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets a value indicating whether the document has a fillable form.</summary>
    public bool HasForm => Filler?.HasForm == true;

    /// <summary>Gets the text field being edited, or <see langword="null"/>.</summary>
    public FormField? Editing
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the text in the open editor.</summary>
    public string EditText
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

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
                return true;
            }

            case FormFieldKind.RadioButton:
            {
                Refresh(field, Filler?.SetChecked(field.PageIndex, field.Index, true) == true);
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
    }

    /// <summary>Writes the open editor's text into its field and closes the editor.</summary>
    public void Commit()
    {
        if (Editing is not { } field)
        {
            return;
        }

        Editing = null;
        if (!string.Equals(field.Value, EditText, StringComparison.Ordinal))
        {
            Refresh(field, Filler?.SetText(field.PageIndex, field.Index, EditText) == true);
        }
    }

    /// <summary>Closes the editor without changing the field.</summary>
    public void Cancel() => Editing = null;

    /// <summary>Commits the editor and opens the next text field, in page order, as Tab does.</summary>
    /// <returns>The next field, or <see langword="null"/> when there is none.</returns>
    public FormField? CommitAndMoveNext()
    {
        var current = Editing;
        Commit();
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
