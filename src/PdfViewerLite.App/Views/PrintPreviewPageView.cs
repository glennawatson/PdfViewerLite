// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia;
using Avalonia.Controls;
using Avalonia.Interactivity;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Media.Imaging;
using Avalonia.Platform;
using PdfViewerLite.App.ViewModels;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Geometry;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;

namespace PdfViewerLite.App.Views;

/// <summary>One sheet in the print preview: the page on white paper with its caption beneath.</summary>
[DebuggerDisplay("{ViewModel}")]
public sealed class PrintPreviewPageView : ReactiveUI.Avalonia.ReactiveUserControl<PrintPreviewPage>
{
    /// <summary>The longest side of a sheet on screen.</summary>
    private const double SheetSide = 620;

    /// <summary>The space above and below each sheet.</summary>
    private const double SheetGap = 8;

    /// <summary>The dots per inch of a screen at scale 1.</summary>
    private const double ScreenDpi = 96;

    /// <summary>The sheet image.</summary>
    private readonly Image _image = new() { Stretch = Stretch.Fill };

    /// <summary>The caption.</summary>
    private readonly TextBlock _caption = new() { HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, 6, 0, 0) };

    /// <summary>The bindings made while loaded.</summary>
    private MultipleDisposable? _bindings;

    /// <summary>The rendered sheet.</summary>
    private WriteableBitmap? _bitmap;

    /// <summary>Initializes a new instance of the <see cref="PrintPreviewPageView"/> class.</summary>
    public PrintPreviewPageView()
    {
        _caption.Classes.Add("secondary");
        var sheet = new StackPanel { HorizontalAlignment = HorizontalAlignment.Center, Margin = new(0, SheetGap) };
        sheet.Children.Add(new Border { Background = Brushes.White, BoxShadow = BoxShadows.Parse("0 1 4 0 #40000000"), Child = _image });
        sheet.Children.Add(_caption);
        Content = sheet;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _bindings?.Dispose();
        _bindings = null;
        ReleaseBitmap();
    }

    /// <inheritdoc/>
    protected override void OnLoaded(RoutedEventArgs e)
    {
        base.OnLoaded(e);

        // A sheet is an immutable record, so the view follows which one it shows.
        _bindings = [this.WhenAnyValue(static v => v.ViewModel).SubscribeSafe(Show, static error => Trace.TraceError(error.ToString()))];
    }

    /// <inheritdoc/>
    protected override void OnUnloaded(RoutedEventArgs e)
    {
        base.OnUnloaded(e);
        _bindings?.Dispose();
        _bindings = null;
        ReleaseBitmap();
    }

    /// <summary>Renders a sheet as it will print: white paper, annotations included.</summary>
    /// <param name="sheet">The sheet.</param>
    private unsafe void Show(PrintPreviewPage? sheet)
    {
        ReleaseBitmap();
        _caption.Text = sheet?.Caption;
        if (sheet is null || sheet.Document.IsDisposed || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return;
        }

        var width = _image.Width;
        var height = _image.Height;
        var scaling = TopLevel.GetTopLevel(this)?.RenderScaling ?? 1;
        var pixels = new PixelSize(Math.Max(1, (int)Math.Round(width * scaling)), Math.Max(1, (int)Math.Round(height * scaling)));
        _bitmap = new(pixels, new(ScreenDpi * scaling, ScreenDpi * scaling), PixelFormat.Bgra8888, AlphaFormat.Premul);
        using (var buffer = _bitmap.Lock())
        {
            var span = new Span<byte>((void*)buffer.Address, buffer.RowBytes * buffer.Size.Height);
            var info = new PageRenderInfo(sheet.PageIndex, pixels.Width / sheet.Size.Width, PageRotation.None, 0, 0, RenderFlags.Annotations | RenderFlags.Printing);
            _ = sheet.Document.Render(info, new(span, pixels.Width, pixels.Height, buffer.RowBytes));
        }

        _image.Source = _bitmap;
    }

    /// <summary>Reserves the paper's size before the virtualising panel measures a recycled sheet.</summary>
    /// <param name="sheet">The sheet assigned to the view.</param>
    private void SetSheetSize(PrintPreviewPage? sheet)
    {
        if (sheet is null || sheet.Size.Width <= 0 || sheet.Size.Height <= 0)
        {
            return;
        }

        var scale = SheetSide / Math.Max(sheet.Size.Width, sheet.Size.Height);
        _image.Width = sheet.Size.Width * scale;
        _image.Height = sheet.Size.Height * scale;
        _caption.Text = sheet.Caption;
    }

    /// <summary>Releases the rendered sheet.</summary>
    private void ReleaseBitmap()
    {
        _image.Source = null;
        _bitmap?.Dispose();
        _bitmap = null;
    }
}
