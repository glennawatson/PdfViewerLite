// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Input.Platform;
using Avalonia.Media;
using Avalonia.Media.Immutable;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Controls;

/// <summary>
/// Annotation input on the page canvas: right-click menus that depend on what is under the pointer (a selection, an
/// annotation or blank page), drawing, placing notes and text, and the outline of the picked annotation.
/// </summary>
public sealed partial class PageCanvas
{
    /// <summary>The width of the live stroke and the selected annotation outline, in device independent pixels.</summary>
    private const double OverlayPenWidth = 1.5;

    /// <summary>The size of colour swatches in menus.</summary>
    private const double SwatchSize = 12;

    /// <summary>The corner radius of colour swatches.</summary>
    private const double SwatchRadius = 2;

    /// <summary>How far the selected annotation outline sits outside the annotation.</summary>
    private const double OutlineInset = 2;

    /// <summary>Points kept per drawing before a new stroke is needed.</summary>
    private const int MaxStrokePoints = 4096;

    /// <summary>The colour of the live stroke while drawing: the annotation ink colour.</summary>
    private static readonly IPen StrokePen = new ImmutablePen(new ImmutableSolidColorBrush(Color.FromUInt32(0xFF000000U | AnnotationColors.Ink)), OverlayPenWidth);

    /// <summary>The points of the stroke being drawn, in page space.</summary>
    private readonly List<PagePoint> _stroke = [];

    /// <summary>The page the stroke is drawn on, or -1.</summary>
    private int _strokePage = -1;

    /// <summary>The page a click tool was pressed on, or -1.</summary>
    private int _clickPage = -1;

    /// <summary>The commands of the open context menu, released when the next one opens.</summary>
    private MultipleDisposable? _menuCommands;

    /// <summary>Runs a click tool: adds a note, text, a signature or a stamp where the page was clicked.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="clicked">The page.</param>
    /// <param name="point">The point, in page space.</param>
    private static void RunClickTool(DocumentTabViewModel tab, int clicked, PagePoint point)
    {
        switch (tab.Annotations.Tool)
        {
            case AnnotationTool.Note:
            {
                _ = tab.Annotations.AddNoteAsync(clicked, point);
                break;
            }

            case AnnotationTool.Text:
            {
                _ = tab.Annotations.AddTextAsync(clicked, point);
                break;
            }

            case AnnotationTool.PlaceSignature:
            {
                tab.FillAndSign.PlaceSignature(clicked, point);
                break;
            }

            case AnnotationTool.Stamp:
            {
                _ = tab.Annotations.AddStamp(clicked, point);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Handles a left press for the active annotation tool.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="e">The event.</param>
    /// <returns><see langword="true"/> when the press was handled.</returns>
    private bool BeginAnnotationPress(Point position, PointerPressedEventArgs e)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        var annotations = tab.Annotations;
        var page = _layout.HitTest(position.X, position.Y);
        if (BeginShape(tab, page, position, e))
        {
            return true;
        }

        switch (annotations.Tool)
        {
            case AnnotationTool.Draw or AnnotationTool.DrawSignature when page >= 0:
            {
                _stroke.Clear();
                _strokePage = page;
                _stroke.Add(ToPage(tab, page, position));
                e.Pointer.Capture(this);
                return true;
            }

            case AnnotationTool.Note or AnnotationTool.Text or AnnotationTool.PlaceSignature or AnnotationTool.Stamp when page >= 0:
            {
                _clickPage = page;
                return true;
            }

            case AnnotationTool.Select when page >= 0 && TryActivateField(tab, page, position):
            {
                return true;
            }

            case AnnotationTool.Select when page >= 0:
            {
                var hit = annotations.HitTest(page, ToPage(tab, page, position));
                annotations.Select(hit);
                InvalidateVisual();
                return hit is not null && !TryHitTestCharacter(position, out _, out _);
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Adds a point to the stroke being drawn.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when a stroke is being drawn.</returns>
    private bool ContinueStroke(Point position)
    {
        if (ContinueShape(position))
        {
            return true;
        }

        if (_strokePage < 0 || Tab is not { } tab)
        {
            return false;
        }

        if (_stroke.Count < MaxStrokePoints)
        {
            _stroke.Add(ToPage(tab, _strokePage, position));
            InvalidateVisual();
        }

        return true;
    }

    /// <summary>Finishes a drawing or click tool on release.</summary>
    /// <param name="position">The canvas point.</param>
    /// <returns><see langword="true"/> when the release was handled.</returns>
    private bool EndAnnotationPress(Point position)
    {
        if (Tab is not { } tab)
        {
            return false;
        }

        var annotations = tab.Annotations;
        if (EndShape(tab, position))
        {
            return true;
        }

        if (_strokePage >= 0)
        {
            var page = _strokePage;
            _strokePage = -1;
            if (_stroke.Count > 1)
            {
                var kind = annotations.Tool == AnnotationTool.DrawSignature ? AnnotationKind.Signature : AnnotationKind.Ink;
                ReadOnlySpan<int> lengths = [_stroke.Count];
                _ = annotations.AddInk(page, CollectionsMarshal.AsSpan(_stroke), lengths, kind);
            }

            _stroke.Clear();
            InvalidateVisual();
            return true;
        }

        if (_clickPage < 0)
        {
            return false;
        }

        var clicked = _clickPage;
        _clickPage = -1;
        RunClickTool(tab, clicked, ToPage(tab, clicked, position));
        return true;
    }

    /// <summary>Marks the selected text when a markup tool is active, then clears the selection.</summary>
    private void ApplyMarkupTool()
    {
        if (Tab is not { } tab || AnnotationsViewModel.GetMarkupKind(tab.Annotations.Tool) is not { } kind)
        {
            return;
        }

        MarkSelection(tab, kind, tab.Annotations.Color);
    }

    /// <summary>Marks the selected text and clears the selection.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="kind">The markup kind.</param>
    /// <param name="color">The colour.</param>
    private void MarkSelection(DocumentTabViewModel tab, AnnotationKind kind, uint color)
    {
        if (!TryGetSelection(out var start, out var end))
        {
            return;
        }

        var lines = new Dictionary<int, List<PageRect>>();
        for (var page = start.Page; page <= end.Page; page++)
        {
            lines[page] = [.. GetSelectionRects(page)];
        }

        if (tab.Annotations.MarkText(kind, lines, color))
        {
            ClearSelection();
        }
    }

    /// <summary>Opens the right-click menu for whatever is under the pointer.</summary>
    /// <param name="position">The canvas point.</param>
    private void ShowContextMenu(Point position)
    {
        if (Tab is not { } tab)
        {
            return;
        }

        _menuCommands?.Dispose();
        _menuCommands = [];
        var page = _layout.HitTest(position.X, position.Y);
        var annotations = tab.Annotations;
        var hit = page >= 0 ? annotations.HitTest(page, ToPage(tab, page, position)) : null;
        List<Control> items;
        if (TryGetSelection(out _, out _))
        {
            items = SelectionMenu(tab);
        }
        else if (hit is not null)
        {
            annotations.Select(hit);
            items = AnnotationMenu(annotations, hit);
        }
        else if (page >= 0)
        {
            items = PageMenu(annotations, page, ToPage(tab, page, position));
        }
        else
        {
            return;
        }

        var menu = new ContextMenu { ItemsSource = items, Placement = PlacementMode.Pointer };
        menu.Open(this);
        InvalidateVisual();
    }

    /// <summary>Builds the menu for selected text: copy, then the ways to mark it.</summary>
    /// <param name="tab">The tab.</param>
    /// <returns>The items.</returns>
    private List<Control> SelectionMenu(DocumentTabViewModel tab)
    {
        var editable = tab.Annotations.CanAnnotate;
        var highlight = new MenuItem { Header = "_Highlight", IsEnabled = editable, ItemsSource = ColorItems(color => MarkSelection(tab, AnnotationKind.Highlight, color)) };
        return
        [
            Item("_Copy", "Ctrl+C", CopySelection),
            Item("_Read Aloud from Here", null, () => ReadAloudFromSelection(tab)),
            new Separator(),
            highlight,
            Item("_Underline", null, () => MarkSelection(tab, AnnotationKind.Underline, tab.Annotations.Color), editable),
            Item("_Strike Out", null, () => MarkSelection(tab, AnnotationKind.StrikeOut, AnnotationColors.Clay), editable),
            Item("S_quiggly Underline", null, () => MarkSelection(tab, AnnotationKind.Squiggly, AnnotationColors.Clay), editable),
        ];
    }

    /// <summary>Builds the menu for an annotation: its note, its colour and removing it.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The items.</returns>
    private List<Control> AnnotationMenu(AnnotationsViewModel annotations, PageAnnotation annotation)
    {
        var name = AnnotationNames.Get(annotation.Kind);
        var colour = new MenuItem { Header = "C_olour", ItemsSource = ColorItems(color => annotations.Recolor(annotation, color)) };
        return
        [
            new MenuItem { Header = name, IsEnabled = false },
            Item(annotation.Contents.Length > 0 ? "_Edit Note…" : "Add _Note…", null, () => _ = annotations.EditNoteAsync(annotation)),
            Item("_Reply…", null, () => _ = annotations.ReplyAsync(annotation)),
            new MenuItem { Header = "_Status", ItemsSource = StatusItems(annotations, annotation) },
            colour,
            new Separator(),
            Item($"_Delete {name}", "Delete", () => annotations.Delete(annotation)),
        ];
    }

    /// <summary>Builds the menu for blank page: add a note or text where the pointer is.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The items.</returns>
    private List<Control> PageMenu(AnnotationsViewModel annotations, int page, PagePoint point)
    {
        var editable = annotations.CanAnnotate;
        var tab = Tab!;
        return
        [
            Item("_Read Aloud from Here", null, () => ReadAloudFrom(tab, page, point)),
            new Separator(),
            Item("Add _Note Here…", null, () => _ = annotations.AddNoteAsync(page, point), editable),
            Item("Add _Text Here…", null, () => _ = annotations.AddTextAsync(page, point), editable),
            new Separator(),
            Item("_Annotate…", null, () => annotations.IsAnnotating = true, editable),
        ];
    }

    /// <summary>Builds one item per review status.</summary>
    /// <param name="annotations">The annotation state.</param>
    /// <param name="annotation">The comment.</param>
    /// <returns>The items.</returns>
    private List<MenuItem> StatusItems(AnnotationsViewModel annotations, PageAnnotation annotation) =>
    [
        Item("_Accepted", null, () => annotations.SetStatus(annotation, ReviewState.Accepted)),
        Item("_Rejected", null, () => annotations.SetStatus(annotation, ReviewState.Rejected)),
        Item("_Cancelled", null, () => annotations.SetStatus(annotation, ReviewState.Cancelled)),
        Item("C_ompleted", null, () => annotations.SetStatus(annotation, ReviewState.Completed)),
    ];

    /// <summary>Builds one named colour item per annotation colour.</summary>
    /// <param name="apply">What choosing a colour does.</param>
    /// <returns>The items.</returns>
    private List<MenuItem> ColorItems(Action<uint> apply)
    {
        var items = new List<MenuItem>(AnnotationColors.All.Count);
        foreach (var (name, color) in AnnotationColors.All)
        {
            var swatch = new Border { Width = SwatchSize, Height = SwatchSize, CornerRadius = new(SwatchRadius), Background = new SolidColorBrush(Color.FromUInt32(0xFF000000U | color)) };
            var item = Item(name, null, () => apply(color));
            item.Icon = swatch;
            items.Add(item);
        }

        return items;
    }

    /// <summary>Creates a menu item running an action.</summary>
    /// <param name="header">The header, with an access key.</param>
    /// <param name="gesture">The shortcut shown, or <see langword="null"/>.</param>
    /// <param name="action">The action.</param>
    /// <returns>The item.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private MenuItem Item(string header, string? gesture, Action action) => Item(header, gesture, action, true);

    /// <summary>Creates a menu item running an action.</summary>
    /// <param name="header">The header, with an access key.</param>
    /// <param name="gesture">The shortcut shown, or <see langword="null"/>.</param>
    /// <param name="action">The action.</param>
    /// <param name="enabled">Whether the item can be chosen.</param>
    /// <returns>The item.</returns>
    private MenuItem Item(string header, string? gesture, Action action, bool enabled)
    {
        var command = ReactiveCommand.Create(action);
        _menuCommands?.Add(command);
        return new() { Header = header, Command = command, IsEnabled = enabled, InputGesture = gesture is null ? null : KeyGesture.Parse(gesture) };
    }

    /// <summary>Handles Escape (clear the selection and the pick) and Delete (remove the picked annotation).</summary>
    /// <param name="key">The key.</param>
    /// <returns><see langword="true"/> when Delete removed an annotation.</returns>
    private bool HandleAnnotationKey(Key key)
    {
        if (key == Key.Escape)
        {
            ClearSelection();
            Tab?.Annotations.Select(null);
            return false;
        }

        if (key != Key.Delete || Tab?.Annotations is not { Selected: { } selected } annotations)
        {
            return false;
        }

        annotations.Delete(selected);
        return true;
    }

    /// <summary>Copies the selected text to the clipboard.</summary>
    private void CopySelection()
    {
        var text = GetSelectedText();
        if (text.Length > 0 && TopLevel.GetTopLevel(this)?.Clipboard is { } clipboard)
        {
            _ = clipboard.SetTextAsync(text);
        }
    }

    /// <summary>Converts a canvas point to page space on a page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="position">The canvas point.</param>
    /// <returns>The page point.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private PagePoint ToPage(DocumentTabViewModel tab, int page, Point position) =>
        new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(position);

    /// <summary>Draws the stroke being drawn and the outline of the picked annotation.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    private void DrawAnnotationOverlay(DrawingContext context, DocumentTabViewModel tab)
    {
        DrawShapePreview(context, tab);
        if (_strokePage >= 0 && _stroke.Count > 1)
        {
            var transform = new PageTransform(_layout.GetPageBounds(_strokePage), _sizes[_strokePage], tab.Rotation, _layout.Options.Scale);
            var previous = transform.ToCanvas(_stroke[0]);
            for (var i = 1; i < _stroke.Count; i++)
            {
                var next = transform.ToCanvas(_stroke[i]);
                context.DrawLine(StrokePen, previous, next);
                previous = next;
            }
        }

        if (tab.Annotations.Selected is { } selected && selected.PageIndex < _sizes.Length && _currentHitPen is { } pen)
        {
            var transform = new PageTransform(_layout.GetPageBounds(selected.PageIndex), _sizes[selected.PageIndex], tab.Rotation, _layout.Options.Scale);
            context.DrawRectangle(null, pen, transform.ToCanvas(selected.Bounds).Inflate(OutlineInset));
        }
    }
}
