// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Annotation state for one tab: the active tool and colour, the sidebar list, undo, and the edits themselves.
/// Edits go straight to the document; the page's tiles are dropped so it redraws.
/// </summary>
[DebuggerDisplay("{Tool}, {Items.Count} annotations")]
public sealed class AnnotationsViewModel : ReactiveObject
{
    /// <summary>The default text size in points.</summary>
    private const float TextSize = 12;

    /// <summary>The default typed signature size in points.</summary>
    private const float SignatureSize = 24;

    /// <summary>The freehand line width in points.</summary>
    private const float InkWidth = 1.5F;

    /// <summary>How far outside an annotation a click still picks it, in points.</summary>
    private const float HitTolerance = 3;

    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Annotations added this session, most recent last, for undo.</summary>
    private readonly List<(int Page, int Index)> _added = [];

    /// <summary>Annotations read for hit testing, reused.</summary>
    private readonly List<PageAnnotation> _scratch = [];

    /// <summary>Initializes a new instance of the <see cref="AnnotationsViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    public AnnotationsViewModel(DocumentTabViewModel owner)
    {
        _owner = owner;
        SetToolCommand = ReactiveCommand.Create<AnnotationTool>(tool => Tool = Tool == tool ? AnnotationTool.Select : tool);
        SetColorCommand = ReactiveCommand.Create<string>(SetColorByName);
        StartCommand = ReactiveCommand.Create(Start);
        DoneCommand = ReactiveCommand.Create(Done);
        UndoCommand = ReactiveCommand.Create(Undo);
        DeleteCommand = ReactiveCommand.Create<AnnotationItemViewModel?>(item => Delete(item?.Annotation ?? Selected));
        EditNoteCommand = ReactiveCommand.CreateFromTask<AnnotationItemViewModel?>(item => EditNoteAsync(item?.Annotation ?? Selected));
        GoToCommand = ReactiveCommand.Create<AnnotationItemViewModel?>(GoTo);
    }

    /// <summary>Gets the interaction asking the user for text.</summary>
    public Interaction<TextPrompt, string?> PromptInteraction { get; } = new();

    /// <summary>Gets the annotations of the document, for the sidebar.</summary>
    public ObservableCollection<AnnotationItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets a value indicating whether the annotation tool row is shown.</summary>
    public bool IsAnnotating
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the active tool.</summary>
    public AnnotationTool Tool
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsSelectTool));
            this.RaisePropertyChanged(nameof(IsHighlightTool));
            this.RaisePropertyChanged(nameof(IsUnderlineTool));
            this.RaisePropertyChanged(nameof(IsStrikeOutTool));
            this.RaisePropertyChanged(nameof(IsDrawTool));
            this.RaisePropertyChanged(nameof(IsNoteTool));
            this.RaisePropertyChanged(nameof(IsTextTool));
        }
    }

    /// <summary>Gets or sets a value indicating whether the select tool is active.</summary>
    public bool IsSelectTool
    {
        get => Tool == AnnotationTool.Select;
        set => SetTool(value, AnnotationTool.Select);
    }

    /// <summary>Gets or sets a value indicating whether the highlight tool is active.</summary>
    public bool IsHighlightTool
    {
        get => Tool == AnnotationTool.Highlight;
        set => SetTool(value, AnnotationTool.Highlight);
    }

    /// <summary>Gets or sets a value indicating whether the underline tool is active.</summary>
    public bool IsUnderlineTool
    {
        get => Tool == AnnotationTool.Underline;
        set => SetTool(value, AnnotationTool.Underline);
    }

    /// <summary>Gets or sets a value indicating whether the strikeout tool is active.</summary>
    public bool IsStrikeOutTool
    {
        get => Tool == AnnotationTool.StrikeOut;
        set => SetTool(value, AnnotationTool.StrikeOut);
    }

    /// <summary>Gets or sets a value indicating whether the draw tool is active.</summary>
    public bool IsDrawTool
    {
        get => Tool == AnnotationTool.Draw;
        set => SetTool(value, AnnotationTool.Draw);
    }

    /// <summary>Gets or sets a value indicating whether the note tool is active.</summary>
    public bool IsNoteTool
    {
        get => Tool == AnnotationTool.Note;
        set => SetTool(value, AnnotationTool.Note);
    }

    /// <summary>Gets or sets a value indicating whether the text tool is active.</summary>
    public bool IsTextTool
    {
        get => Tool == AnnotationTool.Text;
        set => SetTool(value, AnnotationTool.Text);
    }

    /// <summary>Gets or sets the colour for new highlights, lines and notes, as 0xRRGGBB.</summary>
    public uint Color
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ColorName));
        }
    } = AnnotationColors.Sand;

    /// <summary>Gets the name of the colour, shown beside its swatch.</summary>
    public string ColorName => AnnotationNames.GetColor(Color);

    /// <summary>Gets the annotation picked with the select tool, outlined on the page.</summary>
    public PageAnnotation? Selected
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the document can be annotated.</summary>
    public bool CanAnnotate => Editor is not null;

    /// <summary>Gets a value indicating whether there are edits to undo.</summary>
    public bool CanUndo => _added.Count > 0;

    /// <summary>Gets the command choosing a tool.</summary>
    public ReactiveCommand<AnnotationTool, RxVoid> SetToolCommand { get; }

    /// <summary>Gets the command choosing a colour by name ("Yellow", "Green", "Blue" or "Red").</summary>
    public ReactiveCommand<string, RxVoid> SetColorCommand { get; }

    /// <summary>Gets the command showing the annotation tools.</summary>
    public ReactiveCommand<RxVoid, RxVoid> StartCommand { get; }

    /// <summary>Gets the command hiding the annotation tools.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DoneCommand { get; }

    /// <summary>Gets the command removing the last annotation added.</summary>
    public ReactiveCommand<RxVoid, RxVoid> UndoCommand { get; }

    /// <summary>Gets the command deleting an annotation; null deletes the selected one.</summary>
    public ReactiveCommand<AnnotationItemViewModel?, RxVoid> DeleteCommand { get; }

    /// <summary>Gets the command editing an annotation's note; null edits the selected one.</summary>
    public ReactiveCommand<AnnotationItemViewModel?, RxVoid> EditNoteCommand { get; }

    /// <summary>Gets the command scrolling to an annotation.</summary>
    public ReactiveCommand<AnnotationItemViewModel?, RxVoid> GoToCommand { get; }

    /// <summary>Gets the document's editor, or <see langword="null"/> when the document cannot be edited.</summary>
    private IAnnotationEditor? Editor => _owner.TryGetDocument() as IAnnotationEditor;

    /// <summary>Gets the markup kind a tool creates, or <see langword="null"/> when it does not mark text.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The kind.</returns>
    public static AnnotationKind? GetMarkupKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Highlight => AnnotationKind.Highlight,
        AnnotationTool.Underline => AnnotationKind.Underline,
        AnnotationTool.StrikeOut => AnnotationKind.StrikeOut,
        _ => null,
    };

    /// <summary>Marks the given lines of text on each page.</summary>
    /// <param name="kind">The markup kind.</param>
    /// <param name="linesByPage">The selected line rectangles by page.</param>
    /// <param name="color">The colour.</param>
    /// <returns><see langword="true"/> when anything was marked.</returns>
    public bool MarkText(AnnotationKind kind, IReadOnlyDictionary<int, List<PageRect>> linesByPage, uint color)
    {
        ArgumentNullException.ThrowIfNull(linesByPage);
        if (Editor is not { } editor)
        {
            return false;
        }

        var marked = false;
        foreach (var (page, lines) in linesByPage)
        {
            if (lines.Count == 0)
            {
                continue;
            }

            marked |= Added(page, editor.AddMarkup(page, kind, System.Runtime.InteropServices.CollectionsMarshal.AsSpan(lines), color, string.Empty));
        }

        return marked;
    }

    /// <summary>Adds a freehand drawing or a drawn signature.</summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The points, stroke after stroke.</param>
    /// <param name="strokeLengths">The number of points in each stroke.</param>
    /// <param name="kind">Ink or signature.</param>
    /// <returns><see langword="true"/> when added.</returns>
    public bool AddInk(int page, ReadOnlySpan<PagePoint> points, ReadOnlySpan<int> strokeLengths, AnnotationKind kind) =>
        Editor is { } editor && Added(page, editor.AddInk(page, points, strokeLengths, AnnotationColors.Ink, InkWidth, kind));

    /// <summary>Asks for a note and adds it at a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">Where the note icon goes.</param>
    /// <returns>A task.</returns>
    public async Task AddNoteAsync(int page, PagePoint location)
    {
        var text = await PromptInteraction.Handle(new("Add Note", "Note", string.Empty, "Add Note", true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && Editor is { } editor)
        {
            _ = Added(page, editor.AddNote(page, location, text, Color));
        }
    }

    /// <summary>Asks for text and writes it at a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the text.</param>
    /// <returns>A task.</returns>
    public async Task AddTextAsync(int page, PagePoint location)
    {
        var text = await PromptInteraction.Handle(new("Add Text", "Text to write on the page", string.Empty, "Add Text", true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && Editor is { } editor)
        {
            _ = Added(page, editor.AddText(page, location, text, TextSize, AnnotationColors.Ink, AnnotationKind.TextBox));
        }
    }

    /// <summary>Places a typed signature at a point.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the signature.</param>
    /// <param name="name">The name to sign with.</param>
    /// <returns><see langword="true"/> when placed.</returns>
    public bool PlaceTypedSignature(int page, PagePoint location, string name) =>
        !string.IsNullOrWhiteSpace(name) && Editor is { } editor
        && Added(page, editor.AddText(page, location, name, SignatureSize, AnnotationColors.Ink, AnnotationKind.Signature));

    /// <summary>Finds the annotation under a point, topmost first.</summary>
    /// <param name="page">The page.</param>
    /// <param name="point">The point in page space.</param>
    /// <returns>The annotation, or <see langword="null"/>.</returns>
    public PageAnnotation? HitTest(int page, PagePoint point)
    {
        if (Editor is not { } editor)
        {
            return null;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        for (var i = _scratch.Count - 1; i >= 0; i--)
        {
            var bounds = _scratch[i].Bounds;
            var grown = PageRect.FromEdges(bounds.Left - HitTolerance, bounds.Top - HitTolerance, bounds.Right + HitTolerance, bounds.Bottom + HitTolerance);
            if (grown.Contains(point))
            {
                return _scratch[i];
            }
        }

        return null;
    }

    /// <summary>Picks an annotation, or clears the pick.</summary>
    /// <param name="annotation">The annotation.</param>
    public void Select(PageAnnotation? annotation) => Selected = annotation;

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="color">The colour.</param>
    public void Recolor(PageAnnotation annotation, uint color)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        if (Editor?.SetColor(annotation.PageIndex, annotation.Index, color) == true)
        {
            Edited(annotation.PageIndex);
        }
    }

    /// <summary>Deletes an annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    public void Delete(PageAnnotation? annotation)
    {
        if (annotation is null || Editor?.Remove(annotation.PageIndex, annotation.Index) != true)
        {
            return;
        }

        // Indexes after the removed one shift down; forget undo entries on that page rather than remove the wrong one.
        _ = _added.RemoveAll(entry => entry.Page == annotation.PageIndex);
        Selected = null;
        Edited(annotation.PageIndex);
    }

    /// <summary>Asks for new note text for an annotation.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>A task.</returns>
    public async Task EditNoteAsync(PageAnnotation? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        var text = await PromptInteraction.Handle(new("Edit Note", "Note", annotation.Contents, "Save Note", true)).ToTask().ConfigureAwait(true);
        if (text is not null && Editor?.SetContents(annotation.PageIndex, annotation.Index, text) == true)
        {
            Edited(annotation.PageIndex);
        }
    }

    /// <summary>Rebuilds the sidebar list from every page; called when the annotations panel is shown.</summary>
    public void RefreshItems()
    {
        Items.Clear();
        if (Editor is not { } editor)
        {
            return;
        }

        for (var page = 0; page < _owner.PageCount; page++)
        {
            _scratch.Clear();
            editor.GetAnnotations(page, _scratch);
            foreach (var annotation in _scratch)
            {
                Items.Add(new(annotation, _owner.GetPageDisplay(page)));
            }
        }
    }

    /// <summary>Turns a tool on, or back to selecting when it is turned off.</summary>
    /// <param name="on">Whether the tool is being turned on.</param>
    /// <param name="tool">The tool.</param>
    private void SetTool(bool on, AnnotationTool tool)
    {
        if (on)
        {
            Tool = tool;
        }
        else if (Tool == tool)
        {
            Tool = AnnotationTool.Select;
        }
    }

    /// <summary>Shows the tools; Fill &amp; Sign is put away so only one tool row is ever shown.</summary>
    private void Start()
    {
        _owner.FillAndSign.IsActive = false;
        IsAnnotating = true;
    }

    /// <summary>Hides the tools and goes back to selecting text.</summary>
    private void Done()
    {
        Tool = AnnotationTool.Select;
        IsAnnotating = false;
    }

    /// <summary>Removes the last annotation added.</summary>
    private void Undo()
    {
        if (_added.Count == 0 || Editor is not { } editor)
        {
            return;
        }

        var (page, index) = _added[^1];
        _added.RemoveAt(_added.Count - 1);
        if (editor.Remove(page, index))
        {
            Edited(page);
        }

        this.RaisePropertyChanged(nameof(CanUndo));
    }

    /// <summary>Chooses a colour by its name.</summary>
    /// <param name="name">The name.</param>
    private void SetColorByName(string name)
    {
        foreach (var (colorName, color) in AnnotationColors.All)
        {
            if (!string.Equals(colorName, name, StringComparison.Ordinal))
            {
                continue;
            }

            Color = color;
            if (Selected is { } selected)
            {
                Recolor(selected, color);
            }

            return;
        }
    }

    /// <summary>Scrolls to an annotation and picks it.</summary>
    /// <param name="item">The sidebar item.</param>
    private void GoTo(AnnotationItemViewModel? item)
    {
        if (item is null)
        {
            return;
        }

        Selected = item.Annotation;
        _owner.NavigateTo(new(item.Annotation.PageIndex, item.Annotation.Bounds, 0));
    }

    /// <summary>Records an added annotation for undo and redraws its page.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The new index, or -1.</param>
    /// <returns><see langword="true"/> when one was added.</returns>
    private bool Added(int page, int index)
    {
        if (index < 0)
        {
            return false;
        }

        _added.Add((page, index));
        this.RaisePropertyChanged(nameof(CanUndo));
        Edited(page);
        return true;
    }

    /// <summary>Redraws an edited page and refreshes its entries in the list.</summary>
    /// <param name="page">The page.</param>
    private void Edited(int page)
    {
        _owner.OnPageEdited(page);
        RefreshPage(page);
    }

    /// <summary>Replaces one page's entries in the sidebar list, keeping page order.</summary>
    /// <param name="page">The page.</param>
    private void RefreshPage(int page)
    {
        var insertAt = 0;
        for (var i = Items.Count - 1; i >= 0; i--)
        {
            var itemPage = Items[i].Annotation.PageIndex;
            if (itemPage == page)
            {
                Items.RemoveAt(i);
                insertAt = i;
            }
            else if (itemPage < page && insertAt == 0)
            {
                insertAt = i + 1;
            }
        }

        if (Editor is not { } editor)
        {
            return;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        var label = _owner.GetPageDisplay(page);
        foreach (var annotation in _scratch)
        {
            Items.Insert(Math.Min(insertAt, Items.Count), new(annotation, label));
            insertAt++;
        }
    }
}
