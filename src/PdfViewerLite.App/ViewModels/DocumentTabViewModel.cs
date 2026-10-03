// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using PdfViewerLite.App.Rendering;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using PdfViewerLite.Core.Layout;
using PdfViewerLite.Core.Navigation;
using PdfViewerLite.Core.Rendering;
using PdfViewerLite.Core.Settings;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// One open document. A tab is cheap until it is first shown: the native document is opened on demand through the
/// <see cref="DocumentPool"/> and may be closed again while the tab is in the background.
/// </summary>
[DebuggerDisplay("{FileName}")]
public sealed class DocumentTabViewModel : ReactiveObject, IDisposable
{
    /// <summary>How long after saving a file change notice is taken to be our own save.</summary>
    private const long SelfSaveWindowMilliseconds = 2000;

    /// <summary>The width of tab hover previews, in device independent pixels.</summary>
    private const double PreviewSize = 180;

    /// <summary>The width of sidebar thumbnails in device independent pixels.</summary>
    private const double ThumbnailWidth = 120;

    /// <summary>Percent multiplier.</summary>
    private const double Percent = 100;

    /// <summary>The services.</summary>
    private readonly AppServices _services;

    /// <summary>Emits navigation requests for the canvas.</summary>
    private readonly Signal<NavigationRequest> _navigationRequests = new();

    /// <summary>Emits external links to open.</summary>
    private readonly Signal<Uri> _uriRequests = new();

    /// <summary>Emits when the document must be laid out again.</summary>
    private readonly Signal<RxVoid> _documentChanges = new();

    /// <summary>Emits the index of each page whose content was edited.</summary>
    private readonly Signal<int> _pageEdits = new();

    /// <summary>When the file was last saved from this tab, so the change notice that follows is not taken for an outside edit.</summary>
    private long _savedAt;

    /// <summary>Watches the file for changes once loaded.</summary>
    private IDisposable? _fileWatch;

    /// <summary>How the tab looked before presenting.</summary>
    private PresentationState _beforePresenting;

    /// <summary>Initializes a new instance of the <see cref="DocumentTabViewModel"/> class.</summary>
    /// <param name="source">The document source.</param>
    /// <param name="services">The application services.</param>
    public DocumentTabViewModel(DocumentSource source, AppServices services)
    {
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(services);
        Source = source;
        _services = services;
        FileName = Path.GetFileName(source.FilePath);
        Title = FileName;
        ZoomMode = services.Settings.DefaultZoomMode;
        LayoutMode = services.Settings.DefaultLayoutMode;
        SidebarVisible = services.Settings.ShowSidebar;
        PageTone = services.CurrentTheme.PageTone;

        ZoomInCommand = ReactiveCommand.Create(() => SetZoom(ZoomCalculator.ZoomIn(Zoom)));
        ZoomOutCommand = ReactiveCommand.Create(() => SetZoom(ZoomCalculator.ZoomOut(Zoom)));
        ZoomResetCommand = ReactiveCommand.Create(() => SetZoom(1));
        SetZoomCommand = ReactiveCommand.Create<string>(SetZoomFromText);
        FitWidthCommand = ReactiveCommand.Create(() => ZoomMode = ZoomMode.FitWidth);
        FitPageCommand = ReactiveCommand.Create(() => ZoomMode = ZoomMode.FitPage);
        RotateLeftCommand = ReactiveCommand.Create(() => Rotation = Rotation.CounterClockwise);
        RotateRightCommand = ReactiveCommand.Create(() => Rotation = Rotation.Clockwise);
        SetLayoutCommand = ReactiveCommand.Create<string>(SetLayoutFromText);
        NextPageCommand = ReactiveCommand.Create(() => GoToPage(CurrentPageIndex + 1));
        PreviousPageCommand = ReactiveCommand.Create(() => GoToPage(CurrentPageIndex - 1));
        FirstPageCommand = ReactiveCommand.Create(() => GoToPage(0));
        LastPageCommand = ReactiveCommand.Create(() => GoToPage(PageCount - 1));
        GoBackCommand = ReactiveCommand.Create(GoBack);
        GoForwardCommand = ReactiveCommand.Create(GoForward);
        GoToPageEntryCommand = ReactiveCommand.Create(GoToPageEntry);
        ToggleSidebarCommand = ReactiveCommand.Create(() => SidebarVisible = !SidebarVisible);
        FindCommand = ReactiveCommand.Create(() => Search.Open());
        SubmitPasswordCommand = ReactiveCommand.Create(SubmitPassword);
        ReloadCommand = ReactiveCommand.Create(Reload);
        DismissReloadCommand = ReactiveCommand.Create(() => HasPendingReload = false);
        SaveCommand = ReactiveCommand.Create(() => Save(FilePath));
        PrintCommand = ReactiveCommand.CreateFromTask(PrintAsync);
        PresentCommand = ReactiveCommand.Create(() => SetPresenting(!IsPresenting));
        StopPresentingCommand = ReactiveCommand.Create(() => SetPresenting(false));
        DismissNoticeCommand = ReactiveCommand.Create(() => { Notice = null; });
        SaveAsCommand = ReactiveCommand.CreateFromTask(SaveAsAsync);
    }

    /// <summary>Gets the width of the hover preview of a tab.</summary>
    public static double PreviewWidth => PreviewSize;

    /// <summary>Gets the requests for the canvas to scroll.</summary>
    public IObservable<NavigationRequest> NavigationRequests => _navigationRequests;

    /// <summary>Gets the external links the user activated.</summary>
    public IObservable<Uri> UriRequests => _uriRequests;

    /// <summary>Gets notifications that the document must be laid out again (loaded or reloaded).</summary>
    public IObservable<RxVoid> DocumentChanges => _documentChanges;

    /// <summary>Gets the document source.</summary>
    public DocumentSource Source { get; }

    /// <summary>Gets the render hub.</summary>
    public RenderHub RenderHub => _services.RenderHub;

    /// <summary>Gets the render client used by the page canvas.</summary>
    public RenderClient CanvasClient { get; } = new();

    /// <summary>Gets the render client used by sidebar thumbnails.</summary>
    public RenderClient ThumbnailClient { get; } = new();

    /// <summary>Gets the navigation history.</summary>
    public NavigationHistory History { get; } = new();

    /// <summary>Gets the search state, created on first use.</summary>
    public SearchViewModel Search => field ??= new(this);

    /// <summary>Gets the file path.</summary>
    public string FilePath => Source.FilePath;

    /// <summary>Gets the file name.</summary>
    public string FileName { get; }

    /// <summary>Gets the folder the file is in.</summary>
    public string Folder => Path.GetDirectoryName(Source.FilePath) ?? string.Empty;

    /// <summary>Gets the page shown in the hover preview: the page the tab was last on.</summary>
    public int PreviewPageIndex => Math.Max(0, CurrentPageIndex);

    /// <summary>Gets the height of the hover preview, following the page's shape.</summary>
    public double PreviewHeight
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the hover preview caption, for example "Page 3 of 40".</summary>
    public string PreviewCaption
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the document title.</summary>
    public string Title
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the document has been opened at least once.</summary>
    public bool IsLoaded
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the error shown when the document could not be opened.</summary>
    public string? ErrorMessage
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether a password is required.</summary>
    public bool NeedsPassword
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the password being typed.</summary>
    public string PasswordEntry
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the number of pages.</summary>
    public int PageCount
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the zero based page shown in the middle of the viewport.</summary>
    public int CurrentPageIndex
    {
        get;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            PageEntry = GetPageDisplay(value);
            SelectedThumbnail = value >= 0 && value < Thumbnails.Count ? Thumbnails[value] : null;
        }
    }

    /// <summary>Gets or sets the text in the page number box.</summary>
    public string PageEntry
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = string.Empty;

    /// <summary>Gets the last reported scroll position.</summary>
    public DocumentPosition Position { get; private set; }

    /// <summary>Gets the zoom factor, 1 being 100%.</summary>
    public double Zoom
    {
        get;
        private set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(ZoomText));
        }
    } = 1;

    /// <summary>Gets the zoom as display text.</summary>
    public string ZoomText => string.Create(CultureInfo.CurrentCulture, $"{Math.Round(Zoom * Percent)}%");

    /// <summary>Gets or sets how the zoom is chosen.</summary>
    public ZoomMode ZoomMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the page arrangement.</summary>
    public PageLayoutMode LayoutMode
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets a value indicating whether pages are shown one at a time instead of scrolling continuously.</summary>
    public bool IsPageByPage
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets a value indicating whether the document is being presented: full screen, one page at a time, nothing else on screen.</summary>
    public bool IsPresenting
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the command starting or ending presenting.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PresentCommand { get; }

    /// <summary>Gets the command ending presenting.</summary>
    public ReactiveCommand<RxVoid, RxVoid> StopPresentingCommand { get; }

    /// <summary>Gets or sets the page rotation.</summary>
    public PageRotation Rotation
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the paper and ink colours pages are drawn with.</summary>
    public PageTone PageTone
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    } = PageTone.None;

    /// <summary>Gets or sets a value indicating whether the sidebar is shown.</summary>
    public bool SidebarVisible
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets or sets the sidebar panel.</summary>
    public SidebarMode SidebarMode
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            this.RaisePropertyChanged(nameof(IsThumbnailsMode));
            this.RaisePropertyChanged(nameof(IsOutlineMode));
            this.RaisePropertyChanged(nameof(IsSearchMode));
            this.RaisePropertyChanged(nameof(IsAnnotationsMode));
            this.RaisePropertyChanged(nameof(IsAttachmentsMode));
            if (value == SidebarMode.Annotations)
            {
                Annotations.RefreshItems();
            }
        }
    }

    /// <summary>Gets or sets a value indicating whether the thumbnails panel is shown.</summary>
    public bool IsThumbnailsMode
    {
        get => SidebarMode == SidebarMode.Thumbnails;
        set => SetSidebarMode(value, SidebarMode.Thumbnails);
    }

    /// <summary>Gets or sets a value indicating whether the outline panel is shown.</summary>
    public bool IsOutlineMode
    {
        get => SidebarMode == SidebarMode.Outline;
        set => SetSidebarMode(value, SidebarMode.Outline);
    }

    /// <summary>Gets or sets a value indicating whether the search results panel is shown.</summary>
    public bool IsSearchMode
    {
        get => SidebarMode == SidebarMode.Search;
        set => SetSidebarMode(value, SidebarMode.Search);
    }

    /// <summary>Gets or sets a value indicating whether the annotations panel is shown.</summary>
    public bool IsAnnotationsMode
    {
        get => SidebarMode == SidebarMode.Annotations;
        set => SetSidebarMode(value, SidebarMode.Annotations);
    }

    /// <summary>Gets or sets a value indicating whether the attachments panel is shown.</summary>
    public bool IsAttachmentsMode
    {
        get => SidebarMode == SidebarMode.Attachments;
        set => SetSidebarMode(value, SidebarMode.Attachments);
    }

    /// <summary>Gets the files embedded in the document.</summary>
    public AttachmentsViewModel Attachments => field ??= new(this);

    /// <summary>Gets the annotation state.</summary>
    public AnnotationsViewModel Annotations => field ??= new(this);

    /// <summary>Gets the digital signature state.</summary>
    public SignaturesViewModel Signatures => field ??= new(this);

    /// <summary>Gets the tab's text recognition, created on first use.</summary>
    public TextRecognitionViewModel TextRecognition => field ??= new(this, _services);

    /// <summary>Gets the form filling state.</summary>
    public FormsViewModel Forms => field ??= new(this);

    /// <summary>Gets the Fill &amp; Sign state.</summary>
    public FillAndSignViewModel FillAndSign => field ??= new(this, _services);

    /// <summary>Gets a value indicating whether the document has annotations or form entries that are not saved.</summary>
    public bool HasUnsavedChanges
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the pages whose content changed, for example after annotating, so views redraw them.</summary>
    public IObservable<int> PageEdits => _pageEdits;

    /// <summary>Gets the outline.</summary>
    public IReadOnlyList<OutlineItemViewModel> Outline
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>Gets a value indicating whether the document has an outline.</summary>
    public bool HasOutline => Outline.Count > 0;

    /// <summary>Gets the page thumbnails.</summary>
    public IReadOnlyList<ThumbnailItemViewModel> Thumbnails
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    } = [];

    /// <summary>Gets or sets the selected thumbnail; selecting one scrolls to its page.</summary>
    public ThumbnailItemViewModel? SelectedThumbnail
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (value is not null && value.PageIndex != CurrentPageIndex)
            {
                GoToPage(value.PageIndex);
            }
        }
    }

    /// <summary>Gets or sets the selected outline entry; selecting one navigates to it.</summary>
    public OutlineItemViewModel? SelectedOutlineItem
    {
        get;
        set
        {
            _ = this.RaiseAndSetIfChanged(ref field, value);
            if (value is not null)
            {
                Navigate(value.Target);
            }
        }
    }

    /// <summary>Gets the zoom in command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ZoomInCommand { get; }

    /// <summary>Gets the zoom out command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ZoomOutCommand { get; }

    /// <summary>Gets the command resetting the zoom to 100%.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ZoomResetCommand { get; }

    /// <summary>Gets the command setting the zoom from a percentage string.</summary>
    public ReactiveCommand<string, RxVoid> SetZoomCommand { get; }

    /// <summary>Gets the fit width command.</summary>
    public ReactiveCommand<RxVoid, ZoomMode> FitWidthCommand { get; }

    /// <summary>Gets the fit page command.</summary>
    public ReactiveCommand<RxVoid, ZoomMode> FitPageCommand { get; }

    /// <summary>Gets the rotate anti-clockwise command.</summary>
    public ReactiveCommand<RxVoid, PageRotation> RotateLeftCommand { get; }

    /// <summary>Gets the rotate clockwise command.</summary>
    public ReactiveCommand<RxVoid, PageRotation> RotateRightCommand { get; }

    /// <summary>Gets the command setting the layout from its name.</summary>
    public ReactiveCommand<string, RxVoid> SetLayoutCommand { get; }

    /// <summary>Gets the next page command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> NextPageCommand { get; }

    /// <summary>Gets the previous page command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PreviousPageCommand { get; }

    /// <summary>Gets the first page command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> FirstPageCommand { get; }

    /// <summary>Gets the last page command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> LastPageCommand { get; }

    /// <summary>Gets the history back command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> GoBackCommand { get; }

    /// <summary>Gets the history forward command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> GoForwardCommand { get; }

    /// <summary>Gets the command jumping to the page typed in the page box.</summary>
    public ReactiveCommand<RxVoid, RxVoid> GoToPageEntryCommand { get; }

    /// <summary>Gets the toggle sidebar command.</summary>
    public ReactiveCommand<RxVoid, bool> ToggleSidebarCommand { get; }

    /// <summary>Gets the find command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> FindCommand { get; }

    /// <summary>Gets the command submitting the typed password.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SubmitPasswordCommand { get; }

    /// <summary>Gets the command handing the document, with its annotations and filled fields, to the desktop's print dialog.</summary>
    public ReactiveCommand<RxVoid, RxVoid> PrintCommand { get; }

    /// <summary>Gets the reload command.</summary>
    public ReactiveCommand<RxVoid, RxVoid> ReloadCommand { get; }

    /// <summary>Gets a message about the document that waits until dismissed, for example a failed save.</summary>
    public string? Notice
    {
        get;
        internal set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the command dismissing <see cref="Notice"/>.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DismissNoticeCommand { get; }

    /// <summary>Gets the command saving annotations and form entries into the file.</summary>
    public ReactiveCommand<RxVoid, bool> SaveCommand { get; }

    /// <summary>Gets the command saving a copy under a new name.</summary>
    public ReactiveCommand<RxVoid, RxVoid> SaveAsCommand { get; }

    /// <summary>Gets the interaction asking where to save a copy.</summary>
    public Interaction<string, string?> SaveAsInteraction { get; } = new();

    /// <summary>Gets the command dismissing the "file changed" bar without reloading.</summary>
    public ReactiveCommand<RxVoid, bool> DismissReloadCommand { get; }

    /// <summary>Gets a value indicating whether the file changed on disk and the user chose to be asked before reloading.</summary>
    public bool HasPendingReload
    {
        get;
        private set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Opens the document if needed and loads its structure. Safe to call repeatedly.</summary>
    public void EnsureLoaded()
    {
        if (IsLoaded && Source.IsOpen)
        {
            return;
        }

        try
        {
            _ = Source.Acquire();
        }
        catch (DocumentOpenException ex)
        {
            NeedsPassword = ex.Error == DocumentOpenError.Password;
            ErrorMessage = NeedsPassword ? null : ex.Message;
            return;
        }

        NeedsPassword = false;
        ErrorMessage = null;
        if (!IsLoaded)
        {
            OnFirstLoad();
        }
    }

    /// <summary>Gets the open document, opening it if needed.</summary>
    /// <returns>The document, or <see langword="null"/> when it cannot be opened.</returns>
    public IDocument? TryGetDocument()
    {
        try
        {
            return NeedsPassword || ErrorMessage is not null ? null : Source.Acquire();
        }
        catch (DocumentOpenException)
        {
            return null;
        }
    }

    /// <summary>Scrolls to a page.</summary>
    /// <param name="pageIndex">The zero based page.</param>
    public void GoToPage(int pageIndex)
    {
        if (PageCount == 0)
        {
            return;
        }

        NavigateTo(new(Math.Clamp(pageIndex, 0, PageCount - 1), null, 0));
    }

    /// <summary>Follows a link or outline target.</summary>
    /// <param name="target">The target.</param>
    public void Navigate(LinkTarget target)
    {
        switch (target.Kind)
        {
            case LinkTargetKind.Page:
            {
                var area = target.Location is { } point ? new PageRect(point.X, point.Y, 0, 0) : (PageRect?)null;
                NavigateTo(new(target.PageIndex, area, 0));
                break;
            }

            case LinkTargetKind.Uri when Uri.TryCreate(target.Uri, UriKind.Absolute, out var uri):
            {
                _uriRequests.OnNext(uri);
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Records the current position in history and asks the canvas to scroll.</summary>
    /// <param name="request">The destination.</param>
    public void NavigateTo(NavigationRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        History.Push(Position);
        _navigationRequests.OnNext(request);
    }

    /// <summary>Called by the canvas as the view scrolls.</summary>
    /// <param name="position">The position at the top of the viewport.</param>
    /// <param name="currentPage">The page in the middle of the viewport.</param>
    public void ReportPosition(DocumentPosition position, int currentPage)
    {
        Position = position;
        if (currentPage != CurrentPageIndex)
        {
            CurrentPageIndex = currentPage;
        }
    }

    /// <summary>Called by the canvas when a fit mode resolves to a zoom factor.</summary>
    /// <param name="zoom">The zoom.</param>
    public void ReportZoom(double zoom) => Zoom = zoom;

    /// <summary>Sets an explicit zoom, leaving fit modes.</summary>
    /// <param name="zoom">The zoom factor.</param>
    public void SetZoom(double zoom)
    {
        ZoomMode = ZoomMode.Free;
        Zoom = ZoomCalculator.Clamp(zoom);
    }

    /// <summary>Gets the display label of a page, preferring the document's own page labels.</summary>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The label.</returns>
    public string GetPageDisplay(int pageIndex) =>
        pageIndex >= 0 && pageIndex < Thumbnails.Count ? Thumbnails[pageIndex].Label : (pageIndex + 1).ToString(CultureInfo.CurrentCulture);

    /// <summary>Reloads the document from disk, keeping the current page.</summary>
    public void Reload()
    {
        HasPendingReload = false;
        var page = CurrentPageIndex;
        RenderHub.Cache.RemoveDocument(Source.Id);
        Source.Reload();
        IsLoaded = false;
        EnsureLoaded();
        if (!IsLoaded)
        {
            return;
        }

        GoToPage(page);
        Search.Refresh();
    }

    /// <summary>
    /// Readies the hover preview: opens the document if it is not open (the pool closes the least recently used one
    /// when full) and sizes the preview to the page the tab is on. Called when the preview is about to show.
    /// </summary>
    public void PreparePreview()
    {
        if (TryGetDocument() is null)
        {
            PreviewHeight = 0;
            PreviewCaption = NeedsPassword ? "Password protected" : ErrorMessage ?? "This document could not be opened";
            return;
        }

        var page = Math.Min(PreviewPageIndex, Source.PageCount - 1);
        if (page < 0)
        {
            PreviewHeight = 0;
            PreviewCaption = "No pages";
            return;
        }

        var size = Source.PageSizes[page];
        PreviewHeight = size.Width > 0 ? Math.Round(PreviewWidth * size.Height / size.Width) : PreviewWidth;
        PreviewCaption = $"Page {GetPageDisplay(page)} of {Source.PageCount}";
        this.RaisePropertyChanged(nameof(PreviewPageIndex));
    }

    /// <summary>Drops an edited page's tiles so it redraws, and tells views about it.</summary>
    /// <param name="pageIndex">The page.</param>
    public void OnPageEdited(int pageIndex)
    {
        RenderHub.Cache.RemovePage(Source.Id, pageIndex);
        HasUnsavedChanges = Source.HasUnsavedChanges;
        _pageEdits.OnNext(pageIndex);
    }

    /// <summary>
    /// Saves the document. The new file is written next to the old one and then moved over it, so a failure never
    /// leaves a half written file behind.
    /// </summary>
    /// <param name="path">Where to save.</param>
    /// <returns><see langword="true"/> when saved.</returns>
    public bool Save(string path)
    {
        if (TryGetDocument() is not IAnnotationEditor editor)
        {
            return false;
        }

        var temporary = $"{path}.saving";
        try
        {
            bool saved;
            using (var stream = File.Create(temporary))
            {
                saved = editor.Save(stream);
            }

            if (!saved)
            {
                File.Delete(temporary);
                return false;
            }

            _ = Interlocked.Exchange(ref _savedAt, Environment.TickCount64);
            File.Move(temporary, path, true);
            HasUnsavedChanges = Source.HasUnsavedChanges;
            return true;
        }
        catch (IOException ex)
        {
            Notice = $"Could not save: {ex.Message}";
            return false;
        }
        catch (UnauthorizedAccessException ex)
        {
            Notice = $"Could not save: {ex.Message}";
            return false;
        }
    }

    /// <summary>Starts or ends presenting, keeping the tab's own view to restore afterwards.</summary>
    /// <param name="presenting">Whether to present.</param>
    public void SetPresenting(bool presenting)
    {
        if (presenting == IsPresenting)
        {
            return;
        }

        if (presenting)
        {
            _beforePresenting = new(ZoomMode, Zoom, IsPageByPage, SidebarVisible);
            SidebarVisible = false;
            IsPageByPage = true;
            ZoomMode = ZoomMode.FitPage;
            IsPresenting = true;
            return;
        }

        IsPresenting = false;
        var before = _beforePresenting;
        SidebarVisible = before.SidebarVisible;
        IsPageByPage = before.PageByPage;
        if (before.ZoomMode == ZoomMode.Free)
        {
            SetZoom(before.Zoom);
        }
        else
        {
            ZoomMode = before.ZoomMode;
        }
    }

    /// <summary>
    /// Writes the document as it looks now, including annotations and filled fields, to a new temporary file for
    /// printing. The caller deletes the file.
    /// </summary>
    /// <returns>The file, or <see langword="null"/> when it could not be written.</returns>
    public string? WritePrintCopy()
    {
        if (TryGetDocument() is not IAnnotationEditor editor)
        {
            return null;
        }

        var path = Path.Combine(Path.GetTempPath(), $"pdfviewerlite-print-{Guid.NewGuid():N}.pdf");
        try
        {
            using (var stream = File.Create(path))
            {
                if (editor.Save(stream))
                {
                    return path;
                }
            }

            File.Delete(path);
            return null;
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
    }

    /// <summary>Closes the native document while the tab is in the background; it reopens on demand.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Unload() => RenderHub.Cache.RemoveDocument(Source.Id);

    /// <inheritdoc/>
    public void Dispose()
    {
        _fileWatch?.Dispose();
        Search.Dispose();
        _navigationRequests.Dispose();
        _uriRequests.Dispose();
        _documentChanges.Dispose();
        _pageEdits.Dispose();
        RenderHub.Cache.RemoveDocument(Source.Id);
        _ = CanvasClient.Advance();
        _ = ThumbnailClient.Advance();
    }

    /// <summary>Reads document structure after the first successful open.</summary>
    private void OnFirstLoad()
    {
        var sizes = Source.PageSizes;
        var document = Source.Acquire();
        var title = Source.Metadata?.Title;
        Title = string.IsNullOrWhiteSpace(title) ? FileName : title;
        Outline = OutlineItemViewModel.Create(Source.Outline);
        this.RaisePropertyChanged(nameof(HasOutline));

        var thumbnails = new ThumbnailItemViewModel[sizes.Length];
        for (var i = 0; i < sizes.Length; i++)
        {
            var size = sizes[i];
            var label = document.GetPageLabel(i) ?? (i + 1).ToString(CultureInfo.CurrentCulture);
            var height = size.Width > 0 ? ThumbnailWidth * size.Height / size.Width : ThumbnailWidth;
            thumbnails[i] = new(i, label, ThumbnailWidth, height);
        }

        Thumbnails = thumbnails;
        PageCount = sizes.Length;
        IsLoaded = true;
        PageEntry = GetPageDisplay(CurrentPageIndex);
        _services.RecentDocuments.Add(FilePath);
        Signatures.Refresh();
        Attachments.Refresh();
        WatchFile();
        _documentChanges.OnNext(RxVoid.Default);
    }

    /// <summary>Starts watching the file for changes.</summary>
    private void WatchFile() =>
        _fileWatch ??= FileChanges.Watch(FilePath)
            .ObserveOn(RxSchedulers.MainThreadScheduler)
            .SubscribeSafe(_ => OnFileChanged(), static ex => Trace.TraceError(ex.ToString()));

    /// <summary>Reloads a changed file, or offers to, depending on the user's choice. Unsaved edits are never discarded silently.</summary>
    private void OnFileChanged()
    {
        if (Environment.TickCount64 - Interlocked.Read(ref _savedAt) < SelfSaveWindowMilliseconds)
        {
            return;
        }

        if (_services.Settings.FileChangeAction == FileChangeAction.AskToReload || Source.HasUnsavedChanges)
        {
            HasPendingReload = true;
            return;
        }

        Reload();
    }

    /// <summary>Writes a print copy and hands it to the desktop's print dialog, saying so in the notice when it cannot.</summary>
    /// <returns>A task.</returns>
    private async Task PrintAsync()
    {
        var printer = _services.Platform.Printer;
        if (!printer.IsAvailable)
        {
            Notice = "Printing is not available on this desktop.";
            return;
        }

        if (WritePrintCopy() is not { } copy)
        {
            Notice = "Could not prepare the document for printing.";
            return;
        }

        try
        {
            if (!await printer.PrintAsync(copy, FileName, CancellationToken.None).ConfigureAwait(true))
            {
                Notice = "The print dialog could not be opened.";
            }
        }
        finally
        {
            // The desktop holds its own handle to the file, so the name can go now.
            File.Delete(copy);
        }
    }

    /// <summary>Asks where to save a copy and saves it there.</summary>
    /// <returns>A task.</returns>
    private async Task SaveAsAsync()
    {
        var path = await SaveAsInteraction.Handle(FileName).ToTask().ConfigureAwait(true);
        if (!string.IsNullOrEmpty(path))
        {
            _ = Save(path);
        }
    }

    /// <summary>Goes back in history.</summary>
    private void GoBack()
    {
        if (History.TryGoBack(Position, out var target))
        {
            _navigationRequests.OnNext(new(target.PageIndex, null, target.OffsetFraction));
        }
    }

    /// <summary>Goes forward in history.</summary>
    private void GoForward()
    {
        if (History.TryGoForward(Position, out var target))
        {
            _navigationRequests.OnNext(new(target.PageIndex, null, target.OffsetFraction));
        }
    }

    /// <summary>Jumps to the page typed in the page box, accepting page labels or numbers.</summary>
    private void GoToPageEntry()
    {
        var entry = PageEntry.Trim();
        for (var i = 0; i < Thumbnails.Count; i++)
        {
            if (!string.Equals(Thumbnails[i].Label, entry, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            GoToPage(i);
            return;
        }

        if (int.TryParse(entry, NumberStyles.Integer, CultureInfo.CurrentCulture, out var number))
        {
            GoToPage(number - 1);
        }
        else
        {
            PageEntry = GetPageDisplay(CurrentPageIndex);
        }
    }

    /// <summary>Retries opening with the typed password.</summary>
    private void SubmitPassword()
    {
        Source.Password = PasswordEntry;
        PasswordEntry = string.Empty;
        NeedsPassword = false;
        EnsureLoaded();
    }

    /// <summary>Sets the zoom from a percentage such as "150".</summary>
    /// <param name="text">The percentage.</param>
    private void SetZoomFromText(string text)
    {
        if (double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out var percent) && percent > 0)
        {
            SetZoom(percent / Percent);
        }
    }

    /// <summary>Sets the layout from a <see cref="PageLayoutMode"/> name.</summary>
    /// <param name="text">The name.</param>
    private void SetLayoutFromText(string text)
    {
        if (Enum.TryParse<PageLayoutMode>(text, out var mode))
        {
            LayoutMode = mode;
        }
    }

    /// <summary>Switches the sidebar panel when a toggle is checked.</summary>
    /// <param name="isChecked">Whether the toggle is checked.</param>
    /// <param name="mode">The panel.</param>
    private void SetSidebarMode(bool isChecked, SidebarMode mode)
    {
        if (isChecked)
        {
            SidebarMode = mode;
        }
    }
}
