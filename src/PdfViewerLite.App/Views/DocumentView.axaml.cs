// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Controls.Primitives;
using Avalonia.Controls.Templates;
using Avalonia.Input;
using Avalonia.Interactivity;
using Avalonia.Media;
using Avalonia.Platform.Storage;
using Avalonia.Threading;
using Avalonia.VisualTree;
using PdfViewerLite.App.Controls;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;

namespace PdfViewerLite.App.Views;

/// <summary>A document tab: its tool bars, sidebar and pages. Every binding is made here with ReactiveUI.Binding.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed partial class DocumentView : UserControl, IViewFor<DocumentTabViewModel>
{
    /// <summary>Defines the <see cref="ViewModel"/> property.</summary>
    public static readonly StyledProperty<DocumentTabViewModel?> ViewModelProperty = AvaloniaProperty.Register<DocumentView, DocumentTabViewModel?>(nameof(ViewModel));

    /// <summary>The smallest editor font size.</summary>
    private const double MinFieldFontSize = 10;

    /// <summary>The editor font size as a share of the field height.</summary>
    private const double FieldFontShare = 0.6;

    /// <summary>Opaque alpha in 0xAARRGGBB.</summary>
    private const uint OpaqueAlpha = 0xFF000000U;

    /// <summary>The bindings made while attached.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>Initializes a new instance of the <see cref="DocumentView"/> class.</summary>
    public DocumentView()
    {
        InitializeComponent();
        ThumbnailList.ItemTemplate = new FuncDataTemplate<ThumbnailItemViewModel>((_, _) => new ThumbnailItemView { Tab = ViewModel });
        OutlineTree.ItemTemplate = new FuncTreeDataTemplate<OutlineItemViewModel>(static (_, _) => new OutlineItemView(), static item => item.Children);
        SearchResultList.ItemTemplate = new FuncDataTemplate<SearchResultItemViewModel>(static (_, _) => new SearchResultView());
        AnnotationList.ItemTemplate = new FuncDataTemplate<AnnotationItemViewModel>(static (_, _) => new AnnotationItemView());
        SingleLayoutItem.CommandParameter = "Single";
        DualLayoutItem.CommandParameter = "Dual";
        CoverLayoutItem.CommandParameter = "DualCover";
    }

    /// <inheritdoc/>
    public DocumentTabViewModel? ViewModel
    {
        get => GetValue(ViewModelProperty);
        set => SetValue(ViewModelProperty, value);
    }

    /// <inheritdoc/>
    object? IViewFor.ViewModel
    {
        get => ViewModel;
        set => ViewModel = value as DocumentTabViewModel;
    }

    /// <summary>Focuses the page box.</summary>
    public void FocusPageBox()
    {
        _ = PageBox.Focus();
        PageBox.SelectAll();
    }

    /// <summary>Copies the selected text.</summary>
    /// <returns>The selected text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public string GetSelectedText() => Canvas.GetSelectedText();

    /// <inheritdoc/>
    protected override void OnDataContextChanged(EventArgs e)
    {
        base.OnDataContextChanged(e);
        ViewModel = DataContext as DocumentTabViewModel;
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);
        _bindings = [];
        BindToolBar(_bindings);
        BindAnnotationTools(_bindings);
        BindBars(_bindings);
        BindFind(_bindings);
        BindSidebar(_bindings);
        BindPages(_bindings);
        BindWindowCommands(_bindings);
        BindFieldEditor(_bindings);
        Dispatcher.UIThread.Post(FocusControl, Canvas, DispatcherPriority.Loaded);
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
    }

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Focuses a control passed as dispatcher state.</summary>
    /// <param name="state">The control.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void FocusControl(object? state) => (state as Control)?.Focus();

    /// <summary>Focuses a text box passed as dispatcher state and selects its text.</summary>
    /// <param name="state">The text box.</param>
    private static void FocusAndSelect(object? state)
    {
        if (state is not TextBox box)
        {
            return;
        }

        _ = box.Focus();
        box.SelectAll();
    }

    /// <summary>Converts a nullable toggle state to a plain flag.</summary>
    /// <param name="value">The toggle state.</param>
    /// <returns><see langword="true"/> only when checked.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool IsOn(bool? value) => value == true;

    /// <summary>Describes how many signatures a document has.</summary>
    /// <param name="count">The number of signatures.</param>
    /// <returns>The sentence.</returns>
    private static string DescribeSignatures(int count) =>
        count == 1 ? "This document is digitally signed." : string.Create(CultureInfo.CurrentCulture, $"This document has {count} digital signatures.");

    /// <summary>Creates a stream of key presses of one key in a control.</summary>
    /// <param name="control">The control.</param>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifiers that must be held.</param>
    /// <returns>The presses.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static IObservable<RxVoid> KeyPresses(Control control, Key key, KeyModifiers modifiers) =>
        control.GetObservable(KeyDownEvent, RoutingStrategies.Bubble).Where(args => args.Key == key && args.KeyModifiers == modifiers).Select(static _ => RxVoid.Default);

    /// <summary>Binds the main tool bar.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindToolBar(MultipleDisposable bindings)
    {
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasUnsavedChanges, static v => v.SaveButton.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.AnnotateToggle.IsEnabled));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanAnnotate, static v => v.FillSignToggle.IsEnabled));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.RotateLeftCommand, static v => v.RotateLeftItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.RotateRightCommand, static v => v.RotateRightItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.SingleLayoutItem, Signal.Return("Single")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.DualLayoutItem, Signal.Return("Dual")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetLayoutCommand, static v => v.CoverLayoutItem, Signal.Return("DualCover")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveCommand, static v => v.SaveItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SaveAsCommand, static v => v.SaveAsItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadItem));
        bindings.Add(this.Bind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.GoBackCommand, static v => v.BackButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.GoForwardCommand, static v => v.ForwardButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.PreviousPageCommand, static v => v.PreviousPageButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.NextPageCommand, static v => v.NextPageButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.PageEntry, static v => v.PageBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(KeyPresses(PageBox, Key.Enter, KeyModifiers.None).InvokeCommand(ViewModel, static vm => vm.GoToPageEntryCommand));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.PageCount, static v => v.PageCountText.Text, static count => string.Create(CultureInfo.CurrentCulture, $"of {count}")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ZoomOutCommand, static v => v.ZoomOutButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ZoomInCommand, static v => v.ZoomInButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ZoomText, static v => v.ZoomButton.Content));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FitWidthCommand, static v => v.FitWidthItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FitPageCommand, static v => v.FitPageItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom50Item, Signal.Return("50")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom75Item, Signal.Return("75")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom100Item, Signal.Return("100")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom125Item, Signal.Return("125")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom150Item, Signal.Return("150")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom200Item, Signal.Return("200")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SetZoomCommand, static v => v.Zoom400Item, Signal.Return("400")));
    }

    /// <summary>Binds the Annotate and Fill &amp; Sign tool rows.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindAnnotationTools(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsAnnotating, static v => v.AnnotateBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.UndoCommand, static v => v.UndoButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanUndo, static v => v.UndoButton.IsEnabled));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.DoneCommand, static v => v.AnnotateDoneButton));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsSelectTool, static v => v.SelectTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsHighlightTool, static v => v.HighlightTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderlineTool, static v => v.UnderlineTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsStrikeOutTool, static v => v.StrikeOutTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsDrawTool, static v => v.DrawTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsNoteTool, static v => v.NoteTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsTextTool, static v => v.TextTool.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ColorName, static v => v.ColourText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Color, static v => v.ColourSwatch.Background, static color => new SolidColorBrush(Color.FromUInt32(OpaqueAlpha | color))));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.YellowItem, Signal.Return("Yellow")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.GreenItem, Signal.Return("Green")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.BlueItem, Signal.Return("Blue")));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetColorCommand, static v => v.RedItem, Signal.Return("Red")));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.FillAndSign.IsActive, static v => v.FillSignBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.DoneCommand, static v => v.FillSignDoneButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.DrawSignatureCommand, static v => v.DrawSignatureButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.FillAndSign.TypeSignatureCommand, static v => v.TypeSignatureButton));
        bindings.Add(this.BindInteraction(ViewModel, static vm => vm.Annotations.PromptInteraction, PromptAsync));
        bindings.Add(this.BindInteraction(ViewModel, static vm => vm.SaveAsInteraction, SaveAsAsync));
    }

    /// <summary>Binds the signed, notice and reload bars.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindBars(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.SignatureCount, static v => v.SignedBar.IsVisible, static count => count > 0));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.SignatureCount, static v => v.SignedText.Text, DescribeSignatures));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Signatures.CheckCommand, static v => v.CheckSignaturesButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.IsChecking, static v => v.CheckSignaturesButton.Content, static checking => checking ? "Checking…" : "Check Signatures"));
        bindings.Add(this.BindInteraction(ViewModel, static vm => vm.Signatures.ShowInteraction, ShowSignaturesAsync));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Notice, static v => v.NoticeBar.IsVisible, static notice => notice is not null));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissNoticeCommand, static v => v.DismissNoticeButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.RecognizeCommand, static v => v.RecognizeTextItem));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.TextRecognition.StopCommand, static v => v.StopRecognitionButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.IsRunning, static v => v.RecognitionBar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.ProgressText, static v => v.RecognitionText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.TextRecognition.Progress, static v => v.RecognitionProgress.Value));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasPendingReload, static v => v.ReloadBar.IsVisible));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.ReloadCommand, static v => v.ReloadButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.DismissReloadCommand, static v => v.DismissReloadButton));
    }

    /// <summary>Binds the find bar.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFind(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.IsOpen, static v => v.FindBar.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.Query, static v => v.SearchBox.Text, static query => query, static text => text ?? string.Empty));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.MatchCase, static v => v.MatchCaseToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.WholeWord, static v => v.WholeWordToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.PreviousCommand, static v => v.PreviousResultButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.NextCommand, static v => v.NextResultButton));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.Search.CloseCommand, static v => v.CloseFindButton));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Status, static v => v.SearchStatusText.Text));
        bindings.Add(KeyPresses(SearchBox, Key.Enter, KeyModifiers.None).InvokeCommand(ViewModel, static vm => vm.Search.NextCommand));
        bindings.Add(KeyPresses(SearchBox, Key.Enter, KeyModifiers.Shift).InvokeCommand(ViewModel, static vm => vm.Search.PreviousCommand));
        bindings.Add(KeyPresses(SearchBox, Key.Escape, KeyModifiers.None).InvokeCommand(ViewModel, static vm => vm.Search.CloseCommand));
        bindings.Add(this.WhenAnyValue(static v => v.ViewModel!.Search.IsOpen)
            .Where(static open => open)
            .SubscribeSafe(_ => Dispatcher.UIThread.Post(FocusControl, SearchBox, DispatcherPriority.Loaded), OnError));
    }

    /// <summary>Binds the sidebar panels.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindSidebar(MultipleDisposable bindings)
    {
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.Sidebar.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.SidebarVisible, static v => v.SidebarSplitter.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsOutlineMode, static v => v.OutlineToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.Bind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsToggle.IsChecked, static on => on, IsOn));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsThumbnailsMode, static v => v.ThumbnailList.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Thumbnails, static v => v.ThumbnailList.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.SelectedThumbnail, static v => v.ThumbnailList.SelectedItem, static item => item, static item => item as ThumbnailItemViewModel));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsOutlineMode, static v => v.OutlinePanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Outline, static v => v.OutlineTree.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.SelectedOutlineItem, static v => v.OutlineTree.SelectedItem, static item => item, static item => item as OutlineItemViewModel));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.HasOutline, static v => v.NoOutlineText.IsVisible, static has => !has));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsAnnotationsMode, static v => v.AnnotationsPanel.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Items, static v => v.AnnotationList.ItemsSource));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.Items.Count, static v => v.NoAnnotationsText.IsVisible, static count => count == 0));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.IsSearchMode, static v => v.SearchResultList.IsVisible));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.Search.Results, static v => v.SearchResultList.ItemsSource));
        bindings.Add(this.Bind(ViewModel, static vm => vm.Search.SelectedResult, static v => v.SearchResultList.SelectedItem, static item => item, static item => item as SearchResultItemViewModel));
        bindings.Add(AnnotationList.GetObservable(SelectingItemsControl.SelectionChangedEvent, RoutingStrategies.Bubble)
            .Select(_ => AnnotationList.SelectedItem as AnnotationItemViewModel)
            .Where(static item => item is not null)
            .InvokeCommand(ViewModel, static vm => vm.Annotations.GoToCommand));
        bindings.Add(AnnotationList.GetObservable(ContextRequestedEvent, RoutingStrategies.Bubble).SubscribeSafe(OnAnnotationContextRequested, OnError));
        bindings.Add(ThumbnailList.GetObservable(ScrollViewer.ScrollChangedEvent, RoutingStrategies.Bubble)
            .Where(static args => args.OffsetDelta.Y != 0)
            .SubscribeSafe(_ => OnThumbnailsScrolled(), OnError));
    }

    /// <summary>Binds the page area, the error panel and the password panel.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindPages(MultipleDisposable bindings)
    {
        bindings.Add(this.WhenAnyValue(static v => v.ViewModel).BindTo(this, static v => v.Canvas.Tab));
        bindings.Add(this.WhenAnyObservable(static v => v.ViewModel!.UriRequests).SubscribeSafe(OpenUri, OnError));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorText.Text));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.ErrorMessage, static v => v.ErrorPanel.IsVisible, static message => message is not null));
        bindings.Add(this.OneWayBind(ViewModel, static vm => vm.NeedsPassword, static v => v.PasswordPanel.IsVisible));
        bindings.Add(this.Bind(ViewModel, static vm => vm.PasswordEntry, static v => v.PasswordBox.Text, static entry => entry, static text => text ?? string.Empty));
        bindings.Add(this.BindCommand(ViewModel, static vm => vm.SubmitPasswordCommand, static v => v.UnlockButton));
        bindings.Add(KeyPresses(PasswordBox, Key.Enter, KeyModifiers.None).InvokeCommand(ViewModel, static vm => vm.SubmitPasswordCommand));
    }

    /// <summary>Wires the menu items whose commands belong to the window rather than the tab.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindWindowCommands(MultipleDisposable bindings)
    {
        if (TopLevel.GetTopLevel(this) is not MainWindow window)
        {
            return;
        }

        bindings.Add(window.WhenAnyValue(static w => w.ViewModel).SubscribeSafe(
            main =>
            {
                ShowInFolderItem.Command = main?.ShowInFolderCommand;
                PropertiesItem.Command = main?.PropertiesCommand;
            },
            OnError));
    }

    /// <summary>Binds the editor placed over the text field being filled in.</summary>
    /// <param name="bindings">The bindings.</param>
    private void BindFieldEditor(MultipleDisposable bindings)
    {
        bindings.Add(this.Bind(ViewModel, static vm => vm.Forms.EditText, static v => v.FieldEditor.Text, static text => text, static text => text ?? string.Empty));
        bindings.Add(this.WhenAnyValue(static v => v.ViewModel!.Forms.Editing).SubscribeSafe(ShowFieldEditor, OnError));
        bindings.Add(KeyPresses(FieldEditor, Key.Escape, KeyModifiers.None).SubscribeSafe(_ => ViewModel?.Forms.Cancel(), OnError));
        bindings.Add(FieldEditor.GetObservable(KeyDownEvent, RoutingStrategies.Tunnel).Where(static args => args.Key == Key.Tab).SubscribeSafe(OnFieldTab, OnError));
        bindings.Add(KeyPresses(FieldEditor, Key.Enter, KeyModifiers.None)
            .Where(_ => ViewModel?.Forms.Editing is { IsMultiline: false })
            .SubscribeSafe(_ => ViewModel?.Forms.Commit(), OnError));
        bindings.Add(FieldEditor.GetObservable(LostFocusEvent, RoutingStrategies.Bubble).SubscribeSafe(_ => ViewModel?.Forms.Commit(), OnError));
    }

    /// <summary>Places the editor over a field and focuses it, or hides it.</summary>
    /// <param name="field">The field being edited, or <see langword="null"/>.</param>
    private void ShowFieldEditor(Core.Forms.FormField? field)
    {
        if (field is null)
        {
            FieldEditor.IsVisible = false;
            _ = Canvas.Focus();
            return;
        }

        var rect = Canvas.GetCanvasRect(field.PageIndex, field.Bounds);
        Avalonia.Controls.Canvas.SetLeft(FieldEditor, rect.X);
        Avalonia.Controls.Canvas.SetTop(FieldEditor, rect.Y);
        FieldEditor.Width = rect.Width;
        FieldEditor.Height = rect.Height;
        FieldEditor.AcceptsReturn = field.IsMultiline;
        FieldEditor.FontSize = Math.Max(MinFieldFontSize, rect.Height * FieldFontShare);
        FieldEditor.IsVisible = true;
        Dispatcher.UIThread.Post(FocusAndSelect, FieldEditor, DispatcherPriority.Loaded);
    }

    /// <summary>Moves to the next text field on Tab.</summary>
    /// <param name="e">The key press.</param>
    private void OnFieldTab(KeyEventArgs e)
    {
        e.Handled = true;
        _ = ViewModel?.Forms.CommitAndMoveNext();
    }

    /// <summary>Shows the checked signatures.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task ShowSignaturesAsync(IInteractionContext<SignaturesViewModel, RxVoid> context)
    {
        if (TopLevel.GetTopLevel(this) is Window owner)
        {
            await new SignaturesWindow { ViewModel = context.Input }.ShowDialog(owner);
        }

        context.SetOutput(RxVoid.Default);
    }

    /// <summary>Asks the user for text.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task PromptAsync(IInteractionContext<TextPrompt, string?> context)
    {
        if (TopLevel.GetTopLevel(this) is not Window owner)
        {
            context.SetOutput(null);
            return;
        }

        context.SetOutput(await new PromptWindow { ViewModel = context.Input }.ShowDialog<string?>(owner));
    }

    /// <summary>Asks where to save a copy, through the desktop's save dialog.</summary>
    /// <param name="context">The interaction context, holding the suggested file name.</param>
    /// <returns>A task.</returns>
    private async Task SaveAsAsync(IInteractionContext<string, string?> context)
    {
        if (TopLevel.GetTopLevel(this)?.StorageProvider is not { } storage)
        {
            context.SetOutput(null);
            return;
        }

        var file = await storage.SaveFilePickerAsync(new() { Title = "Save a Copy", SuggestedFileName = context.Input, DefaultExtension = "pdf" });
        context.SetOutput(file?.TryGetLocalPath());
    }

    /// <summary>Opens the menu of the annotation under the pointer in the sidebar: edit its note or delete it.</summary>
    /// <param name="e">The event.</param>
    private void OnAnnotationContextRequested(ContextRequestedEventArgs e)
    {
        if ((e.Source as Control)?.DataContext is not AnnotationItemViewModel item || ViewModel is not { } tab || e.Source is not Control source)
        {
            return;
        }

        var menu = new ContextMenu
        {
            ItemsSource = new Control[]
            {
                new MenuItem { Header = "_Edit Note…", Command = tab.Annotations.EditNoteCommand, CommandParameter = item },
                new MenuItem { Header = "_Delete", Command = tab.Annotations.DeleteCommand, CommandParameter = item },
            },
        };
        menu.Open(source);
        e.Handled = true;
    }

    /// <summary>Opens external links with the desktop's default handler.</summary>
    /// <param name="uri">The URI.</param>
    private void OpenUri(Uri uri)
    {
        if (uri.Scheme is "http" or "https" or "mailto" && TopLevel.GetTopLevel(this)?.Launcher is { } launcher)
        {
            _ = launcher.LaunchUriAsync(uri);
        }
    }

    /// <summary>Drops thumbnail requests for items scrolled out of view and re-requests the visible ones.</summary>
    private void OnThumbnailsScrolled()
    {
        if (ViewModel is not { } tab)
        {
            return;
        }

        _ = tab.ThumbnailClient.Advance();
        foreach (var visual in ThumbnailList.GetVisualDescendants())
        {
            if (visual is PageThumbnail thumbnail)
            {
                thumbnail.InvalidateVisual();
            }
        }
    }
}
