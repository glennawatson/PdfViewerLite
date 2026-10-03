// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Automation.Peers;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Headless;
using Avalonia.VisualTree;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.App.Views;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks every control can be reached by screen readers and the keyboard.</summary>
public sealed class AccessibilityTests
{
    /// <summary>The pages in the test document.</summary>
    private const int Pages = 2;

    /// <summary>The most tab stops followed before giving up.</summary>
    private const int MaxTabStops = 400;

    /// <summary>The window width.</summary>
    private const double WindowWidth = 1280;

    /// <summary>The window height.</summary>
    private const double WindowHeight = 800;

    /// <summary>Every button, toggle, list, text field, check box and slider has a name a screen reader can say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryControlHasAName()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("names.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var tab = main.SelectedTab!;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
            tab.ReadAloud.IsOpen = true;
            tab.Search.IsOpen = true;
            tab.Annotations.IsAnnotating = true;
            tab.FocusMode.IsOn = true;
            _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<FocusView>().Any(static f => f.IsVisible));

            var unnamed = Unnamed(window);

            await Assert.That(string.Join(", ", unnamed)).IsEqualTo(string.Empty);
            tab.ReadAloud.IsOpen = false;
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Every control of the Preferences and Print windows has a name a screen reader can say.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DialogControlsHaveNames()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("dialogs.pdf", Pages)]);
        var preferences = new PreferencesWindow { ViewModel = new(test.Services) };
        using var preview = new PrintPreviewViewModel(main.SelectedTab!, test.Services);
        var print = new PrintPreviewWindow { ViewModel = preview, Width = WindowWidth, Height = WindowHeight };
        preferences.Show();
        print.Show();
        try
        {
            _ = await UiWait.UntilAsync(() => print.IsVisible && preferences.IsVisible);

            await Assert.That(string.Join(", ", Unnamed(preferences))).IsEqualTo(string.Empty);
            await Assert.That(string.Join(", ", Unnamed(print))).IsEqualTo(string.Empty);
        }
        finally
        {
            preferences.Close();
            print.Close();
        }
    }

    /// <summary>
    /// Tab reaches every visible, enabled control and the pages, in order through the tool bar, and comes back round
    /// without getting stuck.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabReachesEveryControl()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        main.Open([test.CreateDocument("keys.pdf", Pages)]);
        var window = new MainWindow { DataContext = main, Width = WindowWidth, Height = WindowHeight };
        window.Show();
        try
        {
            var view = await FindDocumentViewAsync(window);
            var visited = new List<Avalonia.Input.IInputElement>();
            for (var step = 0; step < MaxTabStops; step++)
            {
                window.KeyPress(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Tab, null);
                window.KeyRelease(Avalonia.Input.Key.Tab, Avalonia.Input.RawInputModifiers.None, Avalonia.Input.PhysicalKey.Tab, null);
                Avalonia.Threading.Dispatcher.UIThread.RunJobs();
                var current = window.FocusManager?.GetFocusedElement();
                if (current is null || visited.Contains(current))
                {
                    break;
                }

                visited.Add(current);
            }

            var missed = new List<string>();
            foreach (var visual in view.GetVisualDescendants())
            {
                if (IsInteractive(visual) && visual is Control { TemplatedParent: null, IsEffectivelyVisible: true, IsEffectivelyEnabled: true } control && !visited.Contains(control))
                {
                    missed.Add(control.Name ?? control.GetType().Name);
                }
            }

            var canvas = view.GetVisualDescendants().OfType<Controls.PageCanvas>().Single();
            await Assert.That(string.Join(", ", missed)).IsEqualTo(string.Empty);
            await Assert.That(visited.Contains(canvas)).IsTrue();
            await Assert.That(visited.IndexOf(Find(view, "SidebarToggle"))).IsLessThan(visited.IndexOf(Find(view, "BackButton")));
            await Assert.That(visited.IndexOf(Find(view, "FocusToggle"))).IsLessThan(visited.IndexOf(Find(view, "ReadAloudToggle")));
        }
        finally
        {
            window.Close();
        }
    }

    /// <summary>Lists the interactive controls without an accessible name.</summary>
    /// <param name="root">The window.</param>
    /// <returns>Each one's name or type and its tooltip.</returns>
    internal static List<string> Unnamed(Control root)
    {
        var unnamed = new List<string>();
        foreach (var visual in root.GetVisualDescendants())
        {
            // Controls inside other controls' templates (scroll bar buttons, combo box toggles) are named by their owners.
            if (!IsInteractive(visual) || visual is not Control { TemplatedParent: null } control)
            {
                continue;
            }

            var name = ControlAutomationPeer.CreatePeerForElement(control).GetName();
            if (string.IsNullOrWhiteSpace(name))
            {
                unnamed.Add(control.Name ?? control.GetType().Name);
            }
        }

        return unnamed;
    }

    /// <summary>Waits for the document view to appear.</summary>
    /// <param name="window">The window.</param>
    /// <returns>The view.</returns>
    private static async Task<DocumentView> FindDocumentViewAsync(MainWindow window)
    {
        _ = await UiWait.UntilAsync(() => window.GetVisualDescendants().OfType<DocumentView>().Any());
        return window.GetVisualDescendants().OfType<DocumentView>().Single();
    }

    /// <summary>Finds a named control.</summary>
    /// <param name="root">Where to look.</param>
    /// <param name="name">The name.</param>
    /// <returns>The control.</returns>
    private static Control Find(Control root, string name) => root.GetVisualDescendants().OfType<Control>().Single(control => control.Name == name);

    /// <summary>Determines whether a visual is a control a person operates.</summary>
    /// <param name="visual">The visual.</param>
    /// <returns><see langword="true"/> for buttons, toggles, lists, text fields, check boxes and sliders.</returns>
    private static bool IsInteractive(Avalonia.Visual visual) => visual is Button or ToggleButton or ComboBox or TextBox or CheckBox or Slider;
}
