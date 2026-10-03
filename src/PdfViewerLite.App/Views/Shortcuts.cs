// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using Avalonia.Input;
using PdfViewerLite.App.ViewModels;

namespace PdfViewerLite.App.Views;

/// <summary>
/// The window's keyboard shortcuts, each mapped to a view model command. Kept in one table so the menus' gesture text
/// and the keys stay in step, and so no XAML binding is needed.
/// </summary>
internal static class Shortcuts
{
    /// <summary>The shortcuts: a gesture and how to find its command.</summary>
    private static readonly (KeyGesture Gesture, Func<MainViewModel, ICommand?> Command)[] Table =
    [
        (new(Key.O, KeyModifiers.Control), static vm => vm.OpenCommand),
        (new(Key.W, KeyModifiers.Control), static vm => vm.CloseTabCommand),
        (new(Key.F4, KeyModifiers.Control), static vm => vm.CloseTabCommand),
        (new(Key.PageDown, KeyModifiers.Control), static vm => vm.NextTabCommand),
        (new(Key.PageUp, KeyModifiers.Control), static vm => vm.PreviousTabCommand),
        (new(Key.T, KeyModifiers.Control | KeyModifiers.Shift), static vm => vm.ReopenClosedTabCommand),
        (new(Key.A, KeyModifiers.Control | KeyModifiers.Shift), static vm => vm.ShowTabFinderCommand),
        (new(Key.I, KeyModifiers.Control), static vm => vm.TogglePageToneCommand),
        (new(Key.OemComma, KeyModifiers.Control), static vm => vm.PreferencesCommand),
        (new(Key.Enter, KeyModifiers.Alt), static vm => vm.PropertiesCommand),
        (new(Key.S, KeyModifiers.Control), static vm => vm.SelectedTab?.SaveCommand),
        (new(Key.S, KeyModifiers.Control | KeyModifiers.Shift), static vm => vm.SelectedTab?.SaveAsCommand),
        (new(Key.P, KeyModifiers.Control), static vm => vm.SelectedTab?.PrintCommand),
        (new(Key.Z, KeyModifiers.Control), static vm => vm.SelectedTab?.Annotations.UndoCommand),
        (new(Key.F, KeyModifiers.Control), static vm => vm.SelectedTab?.FindCommand),
        (new(Key.F3), static vm => vm.SelectedTab?.Search.NextCommand),
        (new(Key.F3, KeyModifiers.Shift), static vm => vm.SelectedTab?.Search.PreviousCommand),
        (new(Key.G, KeyModifiers.Control), static vm => vm.SelectedTab?.Search.NextCommand),
        (new(Key.G, KeyModifiers.Control | KeyModifiers.Shift), static vm => vm.SelectedTab?.Search.PreviousCommand),
        (new(Key.OemPlus, KeyModifiers.Control), static vm => vm.SelectedTab?.ZoomInCommand),
        (new(Key.Add, KeyModifiers.Control), static vm => vm.SelectedTab?.ZoomInCommand),
        (new(Key.OemMinus, KeyModifiers.Control), static vm => vm.SelectedTab?.ZoomOutCommand),
        (new(Key.Subtract, KeyModifiers.Control), static vm => vm.SelectedTab?.ZoomOutCommand),
        (new(Key.D0, KeyModifiers.Control), static vm => vm.SelectedTab?.ZoomResetCommand),
        (new(Key.D1, KeyModifiers.Control), static vm => vm.SelectedTab?.FitPageCommand),
        (new(Key.D2, KeyModifiers.Control), static vm => vm.SelectedTab?.FitWidthCommand),
        (new(Key.Left, KeyModifiers.Control), static vm => vm.SelectedTab?.RotateLeftCommand),
        (new(Key.Right, KeyModifiers.Control), static vm => vm.SelectedTab?.RotateRightCommand),
        (new(Key.Left, KeyModifiers.Alt), static vm => vm.SelectedTab?.GoBackCommand),
        (new(Key.Right, KeyModifiers.Alt), static vm => vm.SelectedTab?.GoForwardCommand),
        (new(Key.Home, KeyModifiers.Control), static vm => vm.SelectedTab?.FirstPageCommand),
        (new(Key.End, KeyModifiers.Control), static vm => vm.SelectedTab?.LastPageCommand),
        (new(Key.F9), static vm => vm.SelectedTab?.ToggleSidebarCommand),
        (new(Key.F5), static vm => vm.SelectedTab?.ReloadCommand),
        (new(Key.F5, KeyModifiers.Shift), static vm => vm.SelectedTab?.PresentCommand),
    ];

    /// <summary>Runs the command of the shortcut a key press matches.</summary>
    /// <param name="viewModel">The view model.</param>
    /// <param name="e">The key press.</param>
    /// <returns><see langword="true"/> when a shortcut ran.</returns>
    internal static bool TryRun(MainViewModel viewModel, KeyEventArgs e)
    {
        foreach (var (gesture, find) in Table)
        {
            if (!gesture.Matches(e) || find(viewModel) is not { } command || !command.CanExecute(null))
            {
                continue;
            }

            command.Execute(null);
            return true;
        }

        return false;
    }
}
