// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Annotation state for one tab: the active tool and colour, the sidebar list, undo, and the edits themselves.
/// Edits go straight to the document; the page's tiles are dropped so it redraws.
/// </summary>
[DebuggerDisplay("{Tool}, {Items.Count} annotations")]
public sealed partial class AnnotationsViewModel : ReactiveObject
{
    /// <summary>The default text size in points.</summary>
    private const float TextSize = 12;

    /// <summary>The coordinates stored for each drawn signature point.</summary>
    private const int PointCoordinates = 2;

    /// <summary>The freehand line width in points.</summary>
    private const float InkWidth = 1.5F;

    /// <summary>The size of a kept measurement's label.</summary>
    private const float MeasurementTextSize = 9;

    /// <summary>The title, label and button of the reply prompt.</summary>
    private const string ReplyTitle = "Reply";

    /// <summary>The line width of shapes in points.</summary>
    private const float ShapeWidth = 2;

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
    public AnnotationsViewModel(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets the stamps offered, in menu order.</summary>
    public static IReadOnlyList<string> Stamps { get; } = ["APPROVED", "REVIEWED", "DRAFT", "CONFIDENTIAL", "FINAL", "NOT APPROVED"];

    /// <summary>Gets the interaction asking the user for text.</summary>
    public Interaction<TextPrompt, string?> PromptInteraction { get; } = new();

    /// <summary>Gets the annotations of the document, for the sidebar.</summary>
    public ObservableCollection<AnnotationItemViewModel> Items { get; } = [];

    /// <summary>Gets or sets a value indicating whether the annotation tool row is shown.</summary>
    [Reactive]
    public partial bool IsAnnotating { get; set; }

    /// <summary>Gets or sets the active tool.</summary>
    [Reactive(
        nameof(IsSelectTool),
        nameof(IsHighlightTool),
        nameof(IsUnderlineTool),
        nameof(IsStrikeOutTool),
        nameof(IsDrawTool),
        nameof(IsNoteTool),
        nameof(IsTextTool),
        nameof(ShapeLabel),
        nameof(StampButtonLabel))]
    public partial AnnotationTool Tool { get; set; }

    /// <summary>Gets or sets a value indicating whether the select tool is active.</summary>
    public bool IsSelectTool
    {
        get => Tool == AnnotationTool.Select;
        set => SelectTool(value, AnnotationTool.Select);
    }

    /// <summary>Gets or sets a value indicating whether the highlight tool is active.</summary>
    public bool IsHighlightTool
    {
        get => Tool == AnnotationTool.Highlight;
        set => SelectTool(value, AnnotationTool.Highlight);
    }

    /// <summary>Gets or sets a value indicating whether the underline tool is active.</summary>
    public bool IsUnderlineTool
    {
        get => Tool == AnnotationTool.Underline;
        set => SelectTool(value, AnnotationTool.Underline);
    }

    /// <summary>Gets or sets a value indicating whether the strikeout tool is active.</summary>
    public bool IsStrikeOutTool
    {
        get => Tool == AnnotationTool.StrikeOut;
        set => SelectTool(value, AnnotationTool.StrikeOut);
    }

    /// <summary>Gets or sets a value indicating whether the draw tool is active.</summary>
    public bool IsDrawTool
    {
        get => Tool == AnnotationTool.Draw;
        set => SelectTool(value, AnnotationTool.Draw);
    }

    /// <summary>Gets or sets a value indicating whether the note tool is active.</summary>
    public bool IsNoteTool
    {
        get => Tool == AnnotationTool.Note;
        set => SelectTool(value, AnnotationTool.Note);
    }

    /// <summary>Gets or sets a value indicating whether the text tool is active.</summary>
    public bool IsTextTool
    {
        get => Tool == AnnotationTool.Text;
        set => SelectTool(value, AnnotationTool.Text);
    }

    /// <summary>Gets the shape button's label: the shape being drawn, or "Shape".</summary>
    public string ShapeLabel => GetShapeKind(Tool) is { } kind ? AnnotationNames.Get(kind) : "Shape";

    /// <summary>Gets or sets the word on the stamp placed by the stamp tool.</summary>
    [Reactive(nameof(StampButtonLabel))]
    public partial string StampLabel { get; set; } = "APPROVED";

    /// <summary>Gets the stamp button's label: the stamp being placed, or "Stamp".</summary>
    public string StampButtonLabel => Tool == AnnotationTool.Stamp ? $"Stamp: {StampLabel}" : "Stamp";

    /// <summary>Gets or sets the colour for new highlights, lines and notes, as 0xRRGGBB.</summary>
    [Reactive(nameof(ColorName))]
    public partial uint Color { get; set; } = AnnotationColors.Sand;

    /// <summary>Gets the name of the colour, shown beside its swatch.</summary>
    public string ColorName => AnnotationNames.GetColor(Color);

    /// <summary>Gets the annotation picked with the select tool, outlined on the page.</summary>
    [Reactive]
    public partial PageAnnotation? Selected { get; private set; }

    /// <summary>Gets a value indicating whether the document can be annotated.</summary>
    public bool CanAnnotate => Editor is not null;

    /// <summary>Gets a value indicating whether there are edits to undo.</summary>
    [Reactive]
    public partial bool CanUndo { get; private set; }

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

    /// <summary>Gets the shape a tool draws, or <see langword="null"/> when it does not draw a shape.</summary>
    /// <param name="tool">The tool.</param>
    /// <returns>The kind.</returns>
    public static AnnotationKind? GetShapeKind(AnnotationTool tool) => tool switch
    {
        AnnotationTool.Rectangle => AnnotationKind.Rectangle,
        AnnotationTool.Ellipse => AnnotationKind.Ellipse,
        AnnotationTool.Arrow => AnnotationKind.Arrow,
        AnnotationTool.Line => AnnotationKind.Line,
        _ => null,
    };

    /// <summary>Draws a shape between two points in the deeper tone of the chosen colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="kind">The shape.</param>
    /// <param name="start">Where the drag started.</param>
    /// <param name="end">Where it ended.</param>
    /// <returns><see langword="true"/> when added.</returns>
    public bool AddShape(int page, AnnotationKind kind, PagePoint start, PagePoint end) =>
        Editor is { } editor && Added(page, editor.AddShape(page, kind, start, end, AnnotationColors.Deep(Color), ShapeWidth));

    /// <summary>Places the chosen stamp at a point in the deeper tone of the chosen colour.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The stamp's top-left corner.</param>
    /// <returns><see langword="true"/> when placed.</returns>
    public bool AddStamp(int page, PagePoint location) =>
        !string.IsNullOrWhiteSpace(StampLabel) && Editor is { } editor && Added(page, editor.AddStamp(page, location, StampLabel, AnnotationColors.Deep(Color)));

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

    /// <summary>
    /// Keeps a measurement on the page: its lines as a drawing whose comment is the measurement, and the measurement
    /// as text beside its last point, so it shows when the page is printed.
    /// </summary>
    /// <param name="page">The page.</param>
    /// <param name="points">The measured points, in page space.</param>
    /// <param name="closed">Whether the shape closes back to its first point, as an area does.</param>
    /// <param name="description">The measurement, such as "Distance 12.4 m at 30°".</param>
    /// <returns><see langword="true"/> when it was added.</returns>
    public bool AddMeasurement(int page, ReadOnlySpan<PagePoint> points, bool closed, string description)
    {
        if (Editor is not { } editor || points.Length < 2)
        {
            return false;
        }

        PagePoint[] path = closed ? [.. points, points[0]] : [.. points];
        ReadOnlySpan<int> lengths = [path.Length];
        var line = editor.AddInk(page, path, lengths, AnnotationColors.Ink, InkWidth, AnnotationKind.Ink);
        if (!Added(page, line))
        {
            return false;
        }

        _ = editor.SetContents(page, line, description);
        _ = Added(page, editor.AddText(page, points[^1], description, MeasurementTextSize, AnnotationColors.Ink, AnnotationKind.TextBox));
        return true;
    }

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

    /// <summary>Writes a signature or initials mark into its bounds as a removable signature annotation, then picks it.</summary>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes, in page space.</param>
    /// <param name="mark">The mark.</param>
    /// <returns>The placed annotation, or <see langword="null"/> when it could not be placed.</returns>
    public PageAnnotation? PlaceMark(int page, PageRect bounds, SignatureMark mark)
    {
        ArgumentNullException.ThrowIfNull(mark);
        if (Editor is not { } editor || !mark.IsValid)
        {
            return null;
        }

        var index = mark.Style switch
        {
            SignatureMarkStyle.Typed => AddTypedMark(editor, page, bounds, mark),
            SignatureMarkStyle.Drawn => AddDrawnMark(editor, page, bounds, mark),
            SignatureMarkStyle.Image when editor is IImageSignatureEditor images => images.AddImageSignature(page, bounds, mark.Pixels.Span, (int)mark.Width, (int)mark.Height),
            _ => -1,
        };
        if (!Added(page, index))
        {
            return null;
        }

        Selected = Find(page, index);
        return Selected;
    }

    /// <summary>Reads one annotation as it is now.</summary>
    /// <param name="page">The page.</param>
    /// <param name="index">The annotation index.</param>
    /// <returns>The annotation, or <see langword="null"/> when there is none at that index.</returns>
    public PageAnnotation? Find(int page, int index)
    {
        if (Editor is not { } editor)
        {
            return null;
        }

        _scratch.Clear();
        editor.GetAnnotations(page, _scratch);
        foreach (var annotation in _scratch)
        {
            if (annotation.Index == index)
            {
                return annotation;
            }
        }

        return null;
    }

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
        CanUndo = _added.Count > 0;
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

    /// <summary>Asks for a reply to a comment and adds it.</summary>
    /// <param name="annotation">The comment.</param>
    /// <returns>A task.</returns>
    public async Task ReplyAsync(PageAnnotation? annotation)
    {
        if (annotation is null)
        {
            return;
        }

        var text = await PromptInteraction.Handle(new(ReplyTitle, ReplyTitle, string.Empty, ReplyTitle, true)).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrWhiteSpace(text) && Editor is { } editor)
        {
            _ = Added(annotation.PageIndex, editor.AddReply(annotation.PageIndex, annotation.Index, text, ReviewState.None));
        }
    }

    /// <summary>Records a review status for a comment.</summary>
    /// <param name="annotation">The comment.</param>
    /// <param name="state">The status.</param>
    /// <returns><see langword="true"/> when recorded.</returns>
    public bool SetStatus(PageAnnotation annotation, ReviewState state)
    {
        ArgumentNullException.ThrowIfNull(annotation);
        return Editor is { } editor && Added(annotation.PageIndex, editor.AddReply(annotation.PageIndex, annotation.Index, string.Empty, state));
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
                Items.Add(CreateItem(editor, annotation, _owner.GetPageDisplay(page)));
            }
        }
    }

    /// <summary>Writes a typed mark's text at the size that fills its bounds, as a signature.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes.</param>
    /// <param name="mark">The typed mark.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private static int AddTypedMark(IAnnotationEditor editor, int page, PageRect bounds, SignatureMark mark)
    {
        var size = (float)SignatureMarkLayout.TypedFontSize(bounds.Height);
        return editor.AddText(page, new(bounds.Left, bounds.Top), mark.Text, size, AnnotationColors.Ink, AnnotationKind.Signature);
    }

    /// <summary>Draws a drawn mark's strokes, scaled into its bounds, as a signature.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">Where the mark goes.</param>
    /// <param name="mark">The drawn mark.</param>
    /// <returns>The new annotation's index, or -1.</returns>
    private static int AddDrawnMark(IAnnotationEditor editor, int page, PageRect bounds, SignatureMark mark)
    {
        var count = mark.Points.Length / PointCoordinates;
        var points = ArrayPool<PagePoint>.Shared.Rent(count);
        try
        {
            SignatureMarkLayout.MapPoints(mark, bounds, points);
            return editor.AddInk(page, points.AsSpan(0, count), mark.StrokeLengths.Span, AnnotationColors.Ink, InkWidth, AnnotationKind.Signature);
        }
        finally
        {
            ArrayPool<PagePoint>.Shared.Return(points);
        }
    }

    /// <summary>Makes a sidebar item for an annotation, with its replies.</summary>
    /// <param name="editor">The editor.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="label">The page label.</param>
    /// <returns>The item.</returns>
    private static AnnotationItemViewModel CreateItem(IAnnotationEditor editor, PageAnnotation annotation, string label)
    {
        var replies = new List<AnnotationReply>();
        editor.GetReplies(annotation.PageIndex, annotation.Index, replies);
        return new(annotation, label, replies);
    }

    /// <summary>Turns a tool on, or back to selecting when it is turned off.</summary>
    /// <param name="on">Whether the tool is being turned on.</param>
    /// <param name="tool">The tool.</param>
    private void SelectTool(bool on, AnnotationTool tool)
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

    /// <summary>Chooses a tool, or goes back to selecting when it is already the active one.</summary>
    /// <param name="tool">The tool.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SetTool(AnnotationTool tool) => Tool = Tool == tool ? AnnotationTool.Select : tool;

    /// <summary>Chooses a stamp and the stamp tool.</summary>
    /// <param name="label">The stamp's word; a blank word keeps the current stamp.</param>
    [ReactiveCommand]
    private void SetStamp(string? label)
    {
        if (!string.IsNullOrWhiteSpace(label))
        {
            StampLabel = label;
        }

        Tool = AnnotationTool.Stamp;
    }

    /// <summary>Shows the tools; Fill &amp; Sign is put away so only one tool row is ever shown.</summary>
    [ReactiveCommand]
    private void Start()
    {
        _owner.FillAndSign.IsActive = false;
        IsAnnotating = true;
    }

    /// <summary>Hides the tools and goes back to selecting text.</summary>
    [ReactiveCommand]
    private void Done()
    {
        Tool = AnnotationTool.Select;
        IsAnnotating = false;
    }

    /// <summary>Deletes an annotation; null deletes the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Delete(AnnotationItemViewModel? item) => Delete(item?.Annotation ?? Selected);

    /// <summary>Edits an annotation's note; null edits the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task EditNote(AnnotationItemViewModel? item) => EditNoteAsync(item?.Annotation ?? Selected);

    /// <summary>Asks for a reply to an annotation; null replies to the selected one.</summary>
    /// <param name="item">The sidebar item.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task Reply(AnnotationItemViewModel? item) => ReplyAsync(item?.Annotation ?? Selected);

    /// <summary>Marks the selected text.</summary>
    /// <param name="request">What to mark, and the lines it covers.</param>
    /// <returns><see langword="true"/> when anything was marked.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool MarkSelection(MarkSelectionRequest request) => MarkText(request.Kind, request.Lines, request.Color);

    /// <summary>Changes an annotation's colour.</summary>
    /// <param name="choice">The annotation and its new colour.</param>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void RecolorAnnotation(AnnotationColorChoice choice) => Recolor(choice.Annotation, choice.Color);

    /// <summary>Records a review status for a comment.</summary>
    /// <param name="choice">The comment and its status.</param>
    /// <returns><see langword="true"/> when recorded.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private bool SetAnnotationStatus(AnnotationStatusChoice choice) => SetStatus(choice.Annotation, choice.State);

    /// <summary>Asks for a note and adds it where the page was clicked.</summary>
    /// <param name="location">The page and point.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task AddNoteHere(PageLocation location) => AddNoteAsync(location.Page, location.Point);

    /// <summary>Asks for text and writes it where the page was clicked.</summary>
    /// <param name="location">The page and point.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task AddTextHere(PageLocation location) => AddTextAsync(location.Page, location.Point);

    /// <summary>Removes the last annotation added.</summary>
    [ReactiveCommand]
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

        CanUndo = _added.Count > 0;
    }

    /// <summary>Chooses a colour by its name ("Yellow", "Green", "Blue" or "Red").</summary>
    /// <param name="name">The name.</param>
    [ReactiveCommand]
    private void SetColor(string name)
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
    [ReactiveCommand]
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
        CanUndo = true;
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
            Items.Insert(Math.Min(insertAt, Items.Count), CreateItem(editor, annotation, label));
            insertAt++;
        }
    }
}
