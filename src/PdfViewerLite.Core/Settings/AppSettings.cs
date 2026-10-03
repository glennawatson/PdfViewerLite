// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Text.Json.Serialization;
using PdfViewerLite.Core.Layout;

namespace PdfViewerLite.Core.Settings;

/// <summary>User preferences persisted between runs.</summary>
[DebuggerDisplay("{ColorScheme}, {PageTone}, cache {TileCacheMegabytes} MB")]
public sealed class AppSettings
{
    /// <summary>Gets or sets the colour scheme.</summary>
    public ColorSchemeChoice ColorScheme { get; set; }

    /// <summary>Gets or sets how pages are coloured.</summary>
    public PageToneChoice PageTone { get; set; }

    /// <summary>Gets or sets a value indicating whether the page tone is applied (Ctrl+I toggles it off for true colours).</summary>
    public bool PageToneEnabled { get; set; } = true;

    /// <summary>Gets or sets how tool bar buttons are labelled.</summary>
    public ToolbarStyle ToolbarStyle { get; set; }

    /// <summary>Gets or sets what happens when an open file changes on disk.</summary>
    public FileChangeAction FileChangeAction { get; set; }

    /// <summary>Gets or sets whether interface transitions play.</summary>
    public MotionPreference Motion { get; set; }

    /// <summary>Gets or sets whether the text cursor blinks.</summary>
    public CaretPreference Caret { get; set; }

    /// <summary>Gets or sets the interface font size in points, or <see langword="null"/> to follow the desktop.</summary>
    public double? InterfaceFontSizePoints { get; set; }

    /// <summary>Gets or sets the name used for typed signatures.</summary>
    public string SignatureName { get; set; } = string.Empty;

    /// <summary>Gets or sets the certificate file last used to sign, so it is offered again.</summary>
    public string SigningCertificatePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the Tesseract languages used to recognise text, for example <c>eng</c> or <c>eng+deu</c>.</summary>
    public string OcrLanguage { get; set; } = "eng";

    /// <summary>Gets or sets a value indicating whether the sidebar is shown.</summary>
    public bool ShowSidebar { get; set; } = true;

    /// <summary>Gets or sets the zoom mode for newly opened documents.</summary>
    public ZoomMode DefaultZoomMode { get; set; } = ZoomMode.FitWidth;

    /// <summary>Gets or sets the page arrangement for newly opened documents.</summary>
    public PageLayoutMode DefaultLayoutMode { get; set; } = PageLayoutMode.Single;

    /// <summary>Gets or sets the rendered tile cache budget in megabytes.</summary>
    public int TileCacheMegabytes { get; set; } = 256;

    /// <summary>Gets or sets how many documents stay open in memory; others are reopened when their tab is selected.</summary>
    public int MaxOpenDocuments { get; set; } = 16;

    /// <summary>Gets or sets a value indicating whether tabs are restored on start.</summary>
    public bool RestoreSession { get; set; } = true;

    /// <summary>Gets or sets the window width.</summary>
    public double WindowWidth { get; set; } = 1200;

    /// <summary>Gets or sets the window height.</summary>
    public double WindowHeight { get; set; } = 850;

    /// <summary>Gets or sets a value indicating whether the window is maximised.</summary>
    public bool WindowMaximized { get; set; }

    /// <summary>Gets the tabs open at exit.</summary>
    [JsonObjectCreationHandling(JsonObjectCreationHandling.Populate)]
    public List<SessionTab> Session { get; } = [];
}
