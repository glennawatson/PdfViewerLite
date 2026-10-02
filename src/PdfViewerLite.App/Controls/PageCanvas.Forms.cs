// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia;
using Avalonia.Controls;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Geometry;

namespace PdfViewerLite.App.Controls;

/// <summary>Form fields on the page canvas: clicking a field fills it, and the editor overlay is placed from here.</summary>
public sealed partial class PageCanvas
{
    /// <summary>Converts a rectangle on a page to canvas coordinates, for placing an editor over a field.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The rectangle in page space.</param>
    /// <returns>The canvas rectangle, or an empty one when the page is not laid out.</returns>
    public Rect GetCanvasRect(int page, PageRect bounds) => Tab is not { } tab || (uint)page >= (uint)_sizes.Length
        ? default
        : new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToCanvas(bounds);

    /// <summary>Fills the field under a click, if there is one.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a field took the click.</returns>
    private bool TryActivateField(DocumentTabViewModel tab, int page, Point position)
    {
        if (tab.Forms.HitTest(page, ToPage(tab, page, position)) is not { } field)
        {
            return false;
        }

        if (!tab.Forms.Activate(field) && field.Kind is FormFieldKind.ComboBox or FormFieldKind.ListBox)
        {
            ShowChoices(tab.Forms, field);
        }

        InvalidateVisual();
        return true;
    }

    /// <summary>Opens a menu of a choice field's options, with the current one ticked.</summary>
    /// <param name="forms">The form state.</param>
    /// <param name="field">The field.</param>
    private void ShowChoices(FormsViewModel forms, FormField field)
    {
        _menuCommands?.Dispose();
        _menuCommands = [];
        var items = new List<Control>(field.Options.Count);
        for (var i = 0; i < field.Options.Count; i++)
        {
            var option = i;
            var item = Item(field.Options[i], null, () => forms.Choose(field, option));
            item.ToggleType = MenuItemToggleType.Radio;
            item.IsChecked = i == field.SelectedOption;
            items.Add(item);
        }

        new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer }.Open(this);
    }

    /// <summary>Determines whether a canvas point is over a fillable field.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when over a field.</returns>
    private bool IsOverField(Point position)
    {
        var page = _layout.HitTest(position.X, position.Y);
        return page >= 0 && Tab is { } tab && tab.Forms.HitTest(page, ToPage(tab, page, position)) is not null;
    }
}
