// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Input;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Media.Immutable;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Navigation;
using PdfViewerLite.Core.Rendering;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Controls;

/// <summary>
/// Draws a document as a continuous strip of pages inside a <see cref="ScrollViewer"/>. Only the tiles intersecting the
/// viewport are drawn or requested; every frame re-requests what it is missing under a fresh render generation so work
/// for regions that scrolled away is dropped by the scheduler before it reaches PDFium.
/// </summary>
[DebuggerDisplay("{Tab}")]
public sealed partial class PageCanvas : Control
{
    /// <summary>Defines the <see cref="Tab"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> TabProperty = AvaloniaProperty.Register<PageCanvas, DocumentTabViewModel?>(nameof(Tab));

    /// <summary>Defines the <see cref="HitBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> HitBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(HitBrush));

    /// <summary>Defines the <see cref="CurrentHitOutline"/> property.</summary>
    public static readonly StyledProperty<IBrush?> CurrentHitOutlineProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(CurrentHitOutline));

    /// <summary>Defines the <see cref="SelectionBrush"/> property.</summary>
    public static readonly StyledProperty<IBrush?> SelectionBrushProperty = AvaloniaProperty.Register<PageCanvas, IBrush?>(nameof(SelectionBrush));

    /// <summary>The gap between pages.</summary>
    private const double PageSpacing = 12;

    /// <summary>The margin around the pages.</summary>
    private const double ContentMargin = 16;

    /// <summary>How far beyond the viewport, in viewport heights, pages are prefetched.</summary>
    private const double PrefetchViewports = 0.75;

    /// <summary>Zoom change per wheel notch.</summary>
    private const double WheelZoomStep = 1.1;

    /// <summary>The hit tolerance for text, in points.</summary>
    private const float TextTolerance = 4F;

    /// <summary>The distance kept to the left of a navigation target.</summary>
    private const double TargetPadding = 24;

    /// <summary>Where in the viewport a navigation target is placed, as a fraction of its height from the top.</summary>
    private const double TargetViewportFraction = 0.3;

    /// <summary>The width of the outline marking the current search hit, so it differs by shape as well as colour.</summary>
    private const double CurrentHitOutlineWidth = 2;

    /// <summary>The page shadow offset.</summary>
    private const double ShadowOffset = 2;

    /// <summary>Half, for finding the middle of the viewport.</summary>
    private const double Half = 0.5;

    /// <summary>The pointer travel that turns a click into a drag.</summary>
    private const double DragThreshold = 3;

    /// <summary>The page shadow brush.</summary>
    private static readonly IBrush ShadowBrush = new SolidColorBrush(Color.FromArgb(0x40, 0, 0, 0));

    /// <summary>The hand cursor for links.</summary>
    private static readonly Cursor HandCursor = new(StandardCursorType.Hand);

    /// <summary>The text cursor.</summary>
    private static readonly Cursor TextCursor = new(StandardCursorType.Ibeam);

    /// <summary>Links by page, loaded lazily.</summary>
    private readonly Dictionary<int, IReadOnlyList<PageLink>> _links = [];

    /// <summary>Selection rectangles by page, computed lazily.</summary>
    private readonly Dictionary<int, List<PageRect>> _selectionRects = [];

    /// <summary>The current layout.</summary>
    private DocumentLayout _layout = DocumentLayout.Empty;

    /// <summary>The page sizes the layout was built from.</summary>
    private PageSize[] _sizes = [];

    /// <summary>The hosting scroll viewer.</summary>
    private ScrollViewer? _scroller;

    /// <summary>The tab currently wired up.</summary>
    private DocumentTabViewModel? _wiredTab;

    /// <summary>Subscriptions to the wired tab.</summary>
    private MultipleDisposable? _tabSubscriptions;

    /// <summary>Subscriptions to the hosting scroll viewer.</summary>
    private MultipleDisposable? _scrollerSubscriptions;

    /// <summary>A position to restore once the layout has been measured.</summary>
    private Action? _pendingScroll;

    /// <summary>The pointer position where a press started.</summary>
    private Point _pressPoint;

    /// <summary>The link under the pointer when it was pressed.</summary>
    private PageLink? _pressedLink;

    /// <summary>Whether a text selection drag is active.</summary>
    private bool _selecting;

    /// <summary>The selection anchor (page, character).</summary>
    private (int Page, int Char) _selectionAnchor = (-1, -1);

    /// <summary>The selection focus (page, character).</summary>
    private (int Page, int Char) _selectionFocus = (-1, -1);

    /// <summary>The pen outlining the current search hit, rebuilt when <see cref="CurrentHitOutline"/> changes.</summary>
    private IPen? _currentHitPen;

    /// <summary>Initializes static members of the <see cref="PageCanvas"/> class.</summary>
    static PageCanvas()
    {
        AffectsRender<PageCanvas>(TabProperty, HitBrushProperty, CurrentHitOutlineProperty, SelectionBrushProperty);
        FocusableProperty.OverrideDefaultValue<PageCanvas>(true);
    }

    /// <summary>Gets or sets the tab to display.</summary>
    public DocumentTabViewModel? Tab
    {
        get => GetValue(TabProperty);
        set => SetValue(TabProperty, value);
    }

    /// <summary>Gets or sets the fill of search hits.</summary>
    public IBrush? HitBrush
    {
        get => GetValue(HitBrushProperty);
        set => SetValue(HitBrushProperty, value);
    }

    /// <summary>Gets or sets the outline of the current search hit.</summary>
    public IBrush? CurrentHitOutline
    {
        get => GetValue(CurrentHitOutlineProperty);
        set => SetValue(CurrentHitOutlineProperty, value);
    }

    /// <summary>Gets or sets the fill of selected text.</summary>
    public IBrush? SelectionBrush
    {
        get => GetValue(SelectionBrushProperty);
        set => SetValue(SelectionBrushProperty, value);
    }

    /// <summary>Gets the selected text.</summary>
    /// <returns>The text, or an empty string.</returns>
    public string GetSelectedText()
    {
        var document = Tab?.TryGetDocument();
        if (document is null || !TryGetSelection(out var start, out var end))
        {
            return string.Empty;
        }

        var builder = new StringBuilder();
        for (var page = start.Page; page <= end.Page; page++)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            if (last >= first)
            {
                _ = builder.Append(document.GetText(page, first, last - first + 1));
            }

            if (page != end.Page)
            {
                _ = builder.AppendLine();
            }
        }

        return builder.ToString();
    }

    /// <summary>Clears the text selection.</summary>
    public void ClearSelection()
    {
        _selectionAnchor = (-1, -1);
        _selectionFocus = (-1, -1);
        _selectionRects.Clear();
        InvalidateVisual();
    }

    /// <inheritdoc/>
    public override void Render(DrawingContext context)
    {
        ArgumentNullException.ThrowIfNull(context);
        var tab = Tab;
        var document = tab?.TryGetDocument();
        if (tab is null || document is null || _scroller is null || _layout.PageCount == 0)
        {
            return;
        }

        var viewport = new Rect(_scroller.Offset.X, _scroller.Offset.Y, _scroller.Viewport.Width, _scroller.Viewport.Height);
        var frame = new FrameContext(tab, document, tab.RenderHub, viewport, TopLevel.GetTopLevel(this)?.RenderScaling ?? 1, tab.CanvasClient.Advance());
        var prefetch = viewport.Height * PrefetchViewports;
        _layout.GetVisiblePages(viewport.Top - prefetch, viewport.Bottom + prefetch, out var first, out var last);
        using (context.PushRenderOptions(new() { BitmapInterpolationMode = BitmapInterpolationMode.HighQuality }))
        {
            for (var page = first; page <= last && page >= 0; page++)
            {
                DrawPage(context, frame, page);
            }
        }

        DrawAnnotationOverlay(context, tab);
        DrawCaret(context, tab);
    }

    /// <inheritdoc/>
    protected override Size MeasureOverride(Size availableSize) => new(_layout.ExtentWidth, _layout.ExtentHeight);

    /// <inheritdoc/>
    protected override void OnAttachedToVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnAttachedToVisualTree(e);
        _scroller = this.FindAncestorOfType<ScrollViewer>();
        if (_scroller is { } scroller)
        {
            _scrollerSubscriptions =
            [
                scroller.GetObservable(ScrollViewer.OffsetProperty).Skip(1).SubscribeSafe(_ => OnScrolled(), OnError),
                scroller.GetObservable(BoundsProperty).Select(static bounds => bounds.Size).DistinctUntilChanged().Skip(1).SubscribeSafe(_ => OnViewportResized(), OnError),

                // The extent changes after a layout pass; apply any pending scroll once the dispatcher is idle again.
                scroller.GetObservable(ScrollViewer.ExtentProperty).ObserveOn(RxSchedulers.MainThreadScheduler).SubscribeSafe(_ => ApplyPendingScroll(), OnError),
            ];
        }

        Wire(Tab);
    }

    /// <inheritdoc/>
    protected override void OnDetachedFromVisualTree(VisualTreeAttachmentEventArgs e)
    {
        base.OnDetachedFromVisualTree(e);
        _scrollerSubscriptions?.Dispose();
        _scrollerSubscriptions = null;
        _scroller = null;
        Wire(null);
    }

    /// <inheritdoc/>
    protected override void OnPropertyChanged(AvaloniaPropertyChangedEventArgs change)
    {
        ArgumentNullException.ThrowIfNull(change);
        base.OnPropertyChanged(change);
        if (change.Property == TabProperty && _scroller is not null)
        {
            Wire(Tab);
        }
        else if (change.Property == CurrentHitOutlineProperty)
        {
            _currentHitPen = CurrentHitOutline is { } outline ? new ImmutablePen(outline.ToImmutable(), CurrentHitOutlineWidth) : null;
        }
    }

    /// <inheritdoc/>
    protected override void OnPointerWheelChanged(PointerWheelEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if ((e.KeyModifiers & KeyModifiers.Control) == 0 && StepPageByPage(-e.Delta.Y * LineStep))
        {
            e.Handled = true;
            return;
        }

        if ((e.KeyModifiers & KeyModifiers.Control) == 0 || Tab is not { } tab)
        {
            base.OnPointerWheelChanged(e);
            return;
        }

        var factor = Math.Pow(WheelZoomStep, e.Delta.Y);
        ZoomAround(tab, tab.Zoom * factor, e.GetPosition(this));
        e.Handled = true;
    }

    /// <inheritdoc/>
    protected override void OnPointerPressed(PointerPressedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerPressed(e);
        var point = e.GetCurrentPoint(this);
        if (point.Properties.IsRightButtonPressed)
        {
            ShowContextMenu(point.Position);
            e.Handled = true;
            return;
        }

        if (!point.Properties.IsLeftButtonPressed)
        {
            return;
        }

        _ = Focus();
        _pressPoint = point.Position;
        if (BeginAnnotationPress(point.Position, e))
        {
            return;
        }

        _pressedLink = HitTestLink(point.Position, out _);
        ClearSelection();
        if (_pressedLink is not null || !TryHitTestCharacter(point.Position, out var page, out var character))
        {
            return;
        }

        _selectionAnchor = (page, character);
        _selectionFocus = (page, character);
        _caret = (page, character);
        _selecting = true;
        e.Pointer.Capture(this);
    }

    /// <inheritdoc/>
    protected override void OnPointerMoved(PointerEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerMoved(e);
        var position = e.GetPosition(this);
        if (ContinueStroke(position))
        {
            return;
        }

        if (_selecting)
        {
            if (TryHitTestCharacter(position, out var page, out var character))
            {
                _selectionFocus = (page, character);
                _selectionRects.Clear();
                InvalidateVisual();
            }

            return;
        }

        var cursor = TryHitTestCharacter(position, out _, out _) ? TextCursor : Cursor.Default;
        if (HitTestLink(position, out _) is not null || IsOverField(position))
        {
            cursor = HandCursor;
        }

        Cursor = cursor;
    }

    /// <inheritdoc/>
    protected override void OnPointerReleased(PointerReleasedEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        base.OnPointerReleased(e);
        e.Pointer.Capture(null);
        if (EndAnnotationPress(e.GetPosition(this)))
        {
            return;
        }

        if (_selecting)
        {
            _selecting = false;
            ApplyMarkupTool();
        }

        var link = _pressedLink;
        _pressedLink = null;
        var travel = e.GetPosition(this) - _pressPoint;
        if (link is { } pressed && Math.Abs(travel.X) < DragThreshold && Math.Abs(travel.Y) < DragThreshold)
        {
            Tab?.Navigate(pressed.Target);
        }
    }

    /// <inheritdoc/>
    protected override void OnKeyDown(KeyEventArgs e)
    {
        ArgumentNullException.ThrowIfNull(e);
        if (e.Key == Key.C && (e.KeyModifiers & KeyModifiers.Control) != 0)
        {
            CopySelection();
            e.Handled = true;
            return;
        }

        if (HandleAnnotationKey(e.Key) || HandleCaretKey(e) || HandlePageByPageKey(e))
        {
            e.Handled = true;
            return;
        }

        base.OnKeyDown(e);
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Draws the low resolution preview of a page, requesting it when missing.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page rectangle.</param>
    /// <param name="visible">Whether the page is visible.</param>
    private static void DrawPreview(DrawingContext context, in FrameContext frame, int page, in Rect rect, bool visible)
    {
        var tab = frame.Tab;
        var key = TileKey.Preview(tab.Source.Id, page, tab.Rotation, tab.PageTone.Id);
        if (frame.Hub.Cache.TryGet(key, out var surface))
        {
            if (visible)
            {
                context.DrawImage(((AvaloniaRenderSurface)surface).Bitmap, new(0, 0, surface.Width, surface.Height), rect);
            }

            return;
        }

        var size = tab.Source.PageSizes[page];
        var scale = TileGrid.GetPreviewScale(size, tab.Rotation);
        TileGrid.GetPagePixelSize(size, tab.Rotation, scale, out var width, out var height);
        frame.Request(key, new(page, scale, tab.Rotation, 0, 0, RenderFlags.Annotations), width, height, visible ? RenderPriority.VisiblePreview : RenderPriority.Prefetch);
    }

    /// <summary>Draws one tile, or requests it when it is not cached.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="grid">The page's tile grid.</param>
    /// <param name="column">The tile column.</param>
    /// <param name="row">The tile row.</param>
    /// <param name="visible">Whether to draw (true) or only request (false).</param>
    private static void DrawTile(DrawingContext context, in FrameContext frame, in TileRange grid, int column, int row, bool visible)
    {
        var tab = frame.Tab;
        var tileSize = TileGrid.TileSize;
        var key = new TileKey(tab.Source.Id, grid.Page, grid.ScaleKey, tab.Rotation, tab.PageTone.Id, (short)column, (short)row);
        TileGrid.GetTileSize(grid.PixelWidth, grid.PixelHeight, column, row, out var tileWidth, out var tileHeight);
        if (!frame.Hub.Cache.TryGet(key, out var surface))
        {
            var info = new PageRenderInfo(grid.Page, grid.Scale, tab.Rotation, column * tileSize, row * tileSize, RenderFlags.Annotations);
            frame.Request(key, info, tileWidth, tileHeight, visible ? RenderPriority.Visible : RenderPriority.Prefetch);
            return;
        }

        if (!visible)
        {
            return;
        }

        var scaling = frame.RenderScaling;
        var destination = new Rect((grid.OriginX + (column * tileSize)) / scaling, (grid.OriginY + (row * tileSize)) / scaling, tileWidth / scaling, tileHeight / scaling);
        context.DrawImage(((AvaloniaRenderSurface)surface).Bitmap, new(0, 0, tileWidth, tileHeight), destination);
    }

    /// <summary>Draws one page: shadow, background, preview, tiles and highlights.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    private void DrawPage(DrawingContext context, in FrameContext frame, int page)
    {
        var bounds = _layout.GetPageBounds(page);
        var rect = new Rect(bounds.X, bounds.Y, bounds.Width, bounds.Height);
        var visible = rect.Intersects(frame.Viewport);
        if (visible)
        {
            context.FillRectangle(ShadowBrush, rect.Translate(new(ShadowOffset, ShadowOffset)));
            context.FillRectangle(PaperBrush.Get(frame.Tab.PageTone), rect);
        }

        DrawPreview(context, frame, page, rect, visible);
        DrawTiles(context, frame, page, rect, visible);
        if (visible)
        {
            DrawHighlights(context, frame.Tab, page, bounds);
        }
    }

    /// <summary>Draws the full resolution tiles of a page that intersect the viewport, requesting missing ones.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="frame">The frame.</param>
    /// <param name="page">The page.</param>
    /// <param name="rect">The page rectangle.</param>
    /// <param name="visible">Whether the page is on screen; off screen pages are only prefetched.</param>
    private void DrawTiles(DrawingContext context, in FrameContext frame, int page, in Rect rect, bool visible)
    {
        var tab = frame.Tab;
        var scaling = frame.RenderScaling;
        var scale = (float)Math.Min(_layout.Options.Scale * scaling, TileGrid.MaxScale);
        TileGrid.GetPagePixelSize(_sizes[page], tab.Rotation, scale, out var pixelWidth, out var pixelHeight);
        var area = visible ? frame.Viewport : frame.Viewport.Inflate(new Thickness(0, frame.Viewport.Height * PrefetchViewports));
        var region = area.Intersect(rect);

        // The preview already has enough resolution for small pages.
        if (pixelWidth <= TileGrid.PreviewWidth || region.Width <= 0 || region.Height <= 0)
        {
            return;
        }

        // Snap the page origin to device pixels so tiles meet without seams.
        var originX = Math.Round(rect.X * scaling);
        var originY = Math.Round(rect.Y * scaling);
        var tileSize = TileGrid.TileSize;
        TileGrid.GetTileCounts(pixelWidth, pixelHeight, out var columns, out var rows);
        var firstColumn = Math.Clamp((int)(((region.X * scaling) - originX) / tileSize), 0, columns - 1);
        var lastColumn = Math.Clamp((int)(((region.Right * scaling) - originX) / tileSize), 0, columns - 1);
        var firstRow = Math.Clamp((int)(((region.Y * scaling) - originY) / tileSize), 0, rows - 1);
        var lastRow = Math.Clamp((int)(((region.Bottom * scaling) - originY) / tileSize), 0, rows - 1);
        var grid = new TileRange(page, TileGrid.ToScaleKey(scale), scale, pixelWidth, pixelHeight, originX, originY);
        for (var row = firstRow; row <= lastRow; row++)
        {
            for (var column = firstColumn; column <= lastColumn; column++)
            {
                DrawTile(context, frame, grid, column, row, visible);
            }
        }
    }

    /// <summary>Draws search hits and the text selection on a page.</summary>
    /// <param name="context">The drawing context.</param>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The page.</param>
    /// <param name="bounds">The page bounds.</param>
    private void DrawHighlights(DrawingContext context, DocumentTabViewModel tab, int page, in LayoutRect bounds)
    {
        var transform = new PageTransform(bounds, _sizes[page], tab.Rotation, _layout.Options.Scale);
        if (tab.Search.GetHits(page) is { } hits)
        {
            var current = tab.Search.CurrentHit;
            var fill = HitBrush;
            foreach (var hit in hits)
            {
                var pen = ReferenceEquals(hit, current) ? _currentHitPen : null;
                foreach (var rect in hit.Bounds)
                {
                    context.DrawRectangle(fill, pen, transform.ToCanvas(rect));
                }
            }
        }

        if (SelectionBrush is not { } selection)
        {
            return;
        }

        foreach (var rect in GetSelectionRects(page))
        {
            context.FillRectangle(selection, transform.ToCanvas(rect));
        }
    }

    /// <summary>Gets the selection rectangles on a page, computing them on first use.</summary>
    /// <param name="page">The page.</param>
    /// <returns>The rectangles.</returns>
    private List<PageRect> GetSelectionRects(int page)
    {
        if (_selectionRects.TryGetValue(page, out var cached))
        {
            return cached;
        }

        var rects = new List<PageRect>();
        var document = Tab?.TryGetDocument();
        if (document is not null && TryGetSelection(out var start, out var end) && page >= start.Page && page <= end.Page)
        {
            var first = page == start.Page ? start.Char : 0;
            var last = page == end.Page ? end.Char : document.GetCharacterCount(page) - 1;
            if (last >= first)
            {
                document.GetTextBounds(page, first, last - first + 1, rects);
            }
        }

        _selectionRects[page] = rects;
        return rects;
    }

    /// <summary>Gets the normalised selection.</summary>
    /// <param name="start">The start.</param>
    /// <param name="end">The end.</param>
    /// <returns><see langword="true"/> when there is a selection.</returns>
    private bool TryGetSelection(out (int Page, int Char) start, out (int Page, int Char) end)
    {
        start = _selectionAnchor;
        end = _selectionFocus;
        if (start.Page < 0 || end.Page < 0 || start == end)
        {
            return false;
        }

        if (end.Page < start.Page || (end.Page == start.Page && end.Char < start.Char))
        {
            (start, end) = (end, start);
        }

        return true;
    }

    /// <summary>Finds the character under a canvas point.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="page">The page.</param>
    /// <param name="character">The character index.</param>
    /// <returns><see langword="true"/> when over text.</returns>
    private bool TryHitTestCharacter(Point position, out int page, out int character)
    {
        character = -1;
        page = _layout.HitTest(position.X, position.Y);
        var tab = Tab;
        var document = tab?.TryGetDocument();
        if (page < 0 || tab is null || document is null)
        {
            return false;
        }

        var transform = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale);
        character = document.GetCharacterIndexAt(page, transform.ToPage(position), TextTolerance);
        return character >= 0;
    }

    /// <summary>Finds the link under a canvas point.</summary>
    /// <param name="position">The canvas point.</param>
    /// <param name="page">The page.</param>
    /// <returns>The link, if any.</returns>
    private PageLink? HitTestLink(Point position, out int page)
    {
        page = _layout.HitTest(position.X, position.Y);
        var tab = Tab;
        if (page < 0 || tab is null)
        {
            return null;
        }

        ref var links = ref CollectionsMarshal.GetValueRefOrAddDefault(_links, page, out _);
        links ??= tab.TryGetDocument()?.GetLinks(page) ?? [];

        var point = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(position);
        foreach (var link in links)
        {
            if (link.Bounds.Contains(point))
            {
                return link;
            }
        }

        return null;
    }

    /// <summary>Subscribes to a tab's changes, replacing any previous subscriptions.</summary>
    /// <param name="tab">The tab.</param>
    private void Wire(DocumentTabViewModel? tab)
    {
        _tabSubscriptions?.Dispose();
        _tabSubscriptions = null;
        if (_wiredTab is { } old)
        {
            _ = old.CanvasClient.Advance();
        }

        _wiredTab = tab;
        _links.Clear();
        ClearSelection();
        if (tab is null)
        {
            _layout = DocumentLayout.Empty;
            return;
        }

        _tabSubscriptions =
        [
            tab.WhenAnyValue(static x => x.ZoomMode, static x => x.LayoutMode, static x => x.Rotation, static x => x.IsPageByPage, static (_, _, _, _) => RxVoid.Default)
                .Skip(1)
                .SubscribeSafe(_ => OnLayoutSettingsChanged(), OnError),
            tab.WhenAnyValue(static x => x.Zoom).Skip(1).Where(_ => tab.ZoomMode == ZoomMode.Free).SubscribeSafe(_ => OnLayoutSettingsChanged(), OnError),
            tab.WhenAnyValue(static x => x.PageTone).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.NavigationRequests.SubscribeSafe(OnNavigationRequested, OnError),
            tab.DocumentChanges.SubscribeSafe(_ => OnDocumentChanged(), OnError),
            tab.Search.HighlightChanges.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.RenderHub.TilesArrived.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.PageEdits.SubscribeSafe(_ => InvalidateVisual(), OnError),
            tab.WhenAnyValue(static x => x.IsCaretMode).Skip(1).SubscribeSafe(on => OnCaretModeChanged(tab, on), OnError),
            tab.Annotations.WhenAnyValue(static x => x.Selected).Skip(1).SubscribeSafe(_ => InvalidateVisual(), OnError),
        ];
        tab.EnsureLoaded();
        var position = tab.Position;
        RebuildLayout();
        ScrollToPosition(position);
    }

    /// <summary>Recomputes the layout from the tab's settings and the viewport.</summary>
    private void RebuildLayout()
    {
        var tab = Tab;
        _sizes = tab?.Source.PageSizes ?? [];
        if (tab is null || _sizes.Length == 0 || _scroller is null)
        {
            _layout = DocumentLayout.Empty;
            InvalidateMeasure();
            return;
        }

        var viewport = _scroller.Bounds.Size;
        var zoom = tab.Zoom;
        if (tab.ZoomMode != ZoomMode.Free)
        {
            zoom = ZoomCalculator.GetFitZoom(_sizes, new(tab.Rotation, tab.LayoutMode, tab.ZoomMode, viewport.Width, viewport.Height, PageSpacing, ContentMargin));
            tab.ReportZoom(zoom);
        }

        _layout = DocumentLayout.Create(
            _sizes,
            new(tab.Rotation, tab.LayoutMode, zoom * ZoomCalculator.PixelsPerPoint, PageSpacing, ContentMargin, viewport.Width) { PageByPage = tab.IsPageByPage, ViewportHeight = viewport.Height });
        InvalidateMeasure();
        InvalidateVisual();
    }

    /// <summary>Zooms keeping the point under the pointer fixed.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="zoom">The new zoom.</param>
    /// <param name="anchor">The canvas point to keep fixed.</param>
    private void ZoomAround(DocumentTabViewModel tab, double zoom, Point anchor)
    {
        if (_scroller is null)
        {
            return;
        }

        var page = _layout.HitTest(anchor.X, anchor.Y);
        if (page < 0)
        {
            page = _layout.GetPageNearest(anchor.Y);
        }

        if (page < 0)
        {
            tab.SetZoom(zoom);
            return;
        }

        var pagePoint = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale).ToPage(anchor);
        var screenOffset = anchor - _scroller.Offset;
        tab.SetZoom(zoom);
        _pendingScroll = () =>
        {
            var transform = new PageTransform(_layout.GetPageBounds(page), _sizes[page], tab.Rotation, _layout.Options.Scale);
            var target = transform.ToCanvas(pagePoint);
            _scroller.Offset = new(target.X - screenOffset.X, target.Y - screenOffset.Y);
        };
        SchedulePendingScroll();
    }

    /// <summary>Scrolls to a remembered position once laid out.</summary>
    /// <param name="position">The position.</param>
    private void ScrollToPosition(DocumentPosition position)
    {
        _pendingScroll = () =>
        {
            if (_scroller is null || position.PageIndex < 0 || position.PageIndex >= _layout.PageCount)
            {
                return;
            }

            _scroller.Offset = new(_scroller.Offset.X, GetPageScrollTop(position.PageIndex, position.OffsetFraction));
        };
        SchedulePendingScroll();
    }

    /// <summary>Applies the pending scroll after the next layout pass, covering layouts whose extent did not change.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void SchedulePendingScroll() => Dispatcher.UIThread.Post(ApplyPendingScroll, DispatcherPriority.Loaded);

    /// <summary>Reports the current position to the tab.</summary>
    private void ReportPosition()
    {
        if (_scroller is null || Tab is not { } tab || _layout.PageCount == 0)
        {
            return;
        }

        var top = _scroller.Offset.Y + ContentMargin;
        var topPage = _layout.GetPageNearest(top);
        var bounds = _layout.GetPageBounds(topPage);
        var fraction = bounds.Height > 0 ? Math.Clamp((top - bounds.Y) / bounds.Height, 0, 1) : 0;
        var middlePage = _layout.GetPageNearest(_scroller.Offset.Y + (_scroller.Viewport.Height * Half));
        tab.ReportPosition(new(topPage, fraction), middlePage);
    }

    /// <summary>Applies a pending scroll once the scroll viewer's extent matches the layout.</summary>
    private void ApplyPendingScroll()
    {
        if (_pendingScroll is null || _scroller is null || Math.Abs(_scroller.Extent.Height - _layout.ExtentHeight) > 1)
        {
            return;
        }

        var scroll = _pendingScroll;
        _pendingScroll = null;
        scroll();
        ReportPosition();
    }

    /// <summary>Repaints and reports the position after scrolling.</summary>
    private void OnScrolled()
    {
        InvalidateVisual();
        if (_pendingScroll is null)
        {
            ReportPosition();
        }
    }

    /// <summary>Relayouts when the viewport size changes, keeping the current position.</summary>
    private void OnViewportResized()
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var position = tab.Position;
        RebuildLayout();
        ScrollToPosition(position);
    }

    /// <summary>Relayouts when zoom, arrangement or rotation change, keeping the current position.</summary>
    private void OnLayoutSettingsChanged()
    {
        if (Tab is not { } tab)
        {
            return;
        }

        var position = tab.Position;
        var hasAnchor = _pendingScroll is not null;
        RebuildLayout();
        if (!hasAnchor)
        {
            ScrollToPosition(position);
        }
    }

    /// <summary>Scrolls to a navigation target.</summary>
    /// <param name="request">The request.</param>
    private void OnNavigationRequested(NavigationRequest request)
    {
        if (_scroller is null || Tab is not { } tab || request.PageIndex < 0 || request.PageIndex >= _layout.PageCount)
        {
            return;
        }

        var bounds = _layout.GetPageBounds(request.PageIndex);
        if (request.Target is { } target)
        {
            var transform = new PageTransform(bounds, _sizes[request.PageIndex], tab.Rotation, _layout.Options.Scale);
            var area = transform.ToCanvas(target);
            var x = area.Width > 0 && (area.X < _scroller.Offset.X || area.Right > _scroller.Offset.X + _scroller.Viewport.Width)
                ? area.X - TargetPadding
                : _scroller.Offset.X;
            _scroller.Offset = new(x, ClampToSlot(request.PageIndex, area.Y - (_scroller.Viewport.Height * TargetViewportFraction)));
        }
        else
        {
            _scroller.Offset = new(_scroller.Offset.X, GetPageScrollTop(request.PageIndex, request.OffsetFraction));
        }

        ReportPosition();
    }

    /// <summary>Relayouts after the document loads or reloads.</summary>
    private void OnDocumentChanged()
    {
        _links.Clear();
        ClearSelection();
        var position = Tab?.Position ?? default;
        RebuildLayout();
        ScrollToPosition(position);
    }
}
