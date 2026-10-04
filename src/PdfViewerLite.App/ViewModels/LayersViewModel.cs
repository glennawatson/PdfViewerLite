// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfViewerLite.Core.Documents;
using ReactiveUI;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// A tab's layers (optional content). The panel only appears for documents that have layers; ticking a layer shows it
/// and clearing it hides it, changing only how the pages look.
/// </summary>
[DebuggerDisplay("{Items.Count} layers")]
public sealed class LayersViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Set while the list is being filled, so filling it does not count as the user changing layers.</summary>
    private bool _refreshing;

    /// <summary>Initializes a new instance of the <see cref="LayersViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public LayersViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the layers.</summary>
    public ObservableCollection<LayerItemViewModel> Items { get; } = [];

    /// <summary>Gets a value indicating whether the document has layers.</summary>
    public bool HasLayers
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Lists the document's layers; called when the document loads.</summary>
    public void Refresh()
    {
        _refreshing = true;
        try
        {
            Items.Clear();
            if (_owner.TryGetDocument() is ILayerSource source)
            {
                foreach (var layer in source.GetLayers())
                {
                    Items.Add(new(this, layer.Id, layer.Name, layer.IsVisible));
                }
            }
        }
        finally
        {
            _refreshing = false;
        }

        HasLayers = Items.Count > 0;
        if (!HasLayers && _owner.IsLayersMode)
        {
            _owner.SidebarMode = SidebarMode.Thumbnails;
        }
    }

    /// <summary>Shows or hides a layer and redraws the pages.</summary>
    /// <param name="item">The layer.</param>
    /// <param name="visible">Whether to show it.</param>
    internal void SetVisible(LayerItemViewModel item, bool visible)
    {
        if (_refreshing || _owner.TryGetDocument() is not ILayerSource source)
        {
            return;
        }

        if (source.SetLayerVisible(item.Id, visible))
        {
            _owner.OnLayersChanged();
        }
    }
}
