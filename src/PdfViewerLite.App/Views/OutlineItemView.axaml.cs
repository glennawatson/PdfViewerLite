// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Views;

/// <summary>One outline entry; keeps the tree item's expansion in step with the view model both ways.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class OutlineItemView : ReactiveUI.Avalonia.ReactiveUserControl<OutlineItemViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="OutlineItemView"/> class.</summary>
    public OutlineItemView()
    {
        InitializeComponent();
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.TitleText.Text));
            disposables.Add(this.WhenChanged(static v => v.ViewModel).SubscribeSafe(vm => ToolTip.SetTip(this, vm?.Title), static error => Trace.TraceError(error.ToString())));

            // The tree item that hosts this view owns the expander, so the model is bound to it directly.
            if (ViewModel is { } viewModel && this.FindAncestorOfType<TreeViewItem>() is { } container)
            {
                disposables.Add(viewModel.BindTwoWay(container, static vm => vm.IsExpanded, static item => item.IsExpanded));
            }
        });
    }
}
