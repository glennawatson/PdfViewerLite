// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using ReactiveUI;

namespace PdfViewerLite.App.ViewModels;

/// <summary>A layer in the layers panel, with a check box showing or hiding it.</summary>
[DebuggerDisplay("{Name} visible={IsVisible}")]
public sealed class LayerItemViewModel : ReactiveObject
{
    /// <summary>The panel, told when the box is ticked or cleared.</summary>
    private readonly LayersViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="LayerItemViewModel"/> class.</summary>
    /// <param name="owner">The panel.</param>
    /// <param name="id">The layer's id.</param>
    /// <param name="name">The layer's name.</param>
    /// <param name="visible">Whether it is shown.</param>
    public LayerItemViewModel(LayersViewModel owner, int id, string name, bool visible)
    {
        _owner = owner;
        Id = id;
        Name = name;
        IsVisible = visible;
    }

    /// <summary>Gets the layer's id.</summary>
    public int Id { get; }

    /// <summary>Gets the layer's name.</summary>
    public string Name { get; }

    /// <summary>Gets or sets a value indicating whether the layer is shown.</summary>
    public bool IsVisible
    {
        get;
        set
        {
            if (field == value)
            {
                return;
            }

            _ = this.RaiseAndSetIfChanged(ref field, value);
            _owner.SetVisible(this, value);
        }
    }
}
