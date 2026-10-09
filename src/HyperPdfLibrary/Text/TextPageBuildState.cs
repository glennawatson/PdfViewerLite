// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Text;

/// <summary>Owns the reusable buffers and current values of a text page build.</summary>
[DebuggerDisplay("TextPageBuildState: {Chars.Count} chars")]
internal sealed record TextPageBuildState
{
    /// <summary>Stores the current Display value.</summary>
    private Matrix3x2 _display;

    /// <summary>Stores the current PageWidth value.</summary>
    private float _pageWidth;

    /// <summary>Stores the current PageHeight value.</summary>
    private float _pageHeight;

    /// <summary>Stores the current RightToLeft value.</summary>
    private bool _rightToLeft;

    /// <summary>Stores the current Previous value.</summary>
    private int _previous = -1;

    /// <summary>Stores the current LineDirection value.</summary>
    private TextOrientation _lineDirection;

    /// <summary>Stores the current LineRect value.</summary>
    private PdfRectangle _lineRect;

    /// <summary>Initializes a new instance of the <see cref="TextPageBuildState"/> class.</summary>
    internal TextPageBuildState()
    {
        Runs = Device.Runs;
        Glyphs = Device.Glyphs;
        Unicode = Device.Unicode;
    }

    /// <summary>Gets the characters of the page, in order.</summary>
    internal List<TextBuildChar> Chars { get; } = [];

    /// <summary>Gets the characters of the line being built.</summary>
    internal List<TextBuildChar> Temp { get; } = [];

    /// <summary>Gets the page text.</summary>
    internal List<char> Text { get; } = [];

    /// <summary>Gets the text of the line being built, one character per entry of <see cref="Temp"/>.</summary>
    internal List<char> TempText { get; } = [];

    /// <summary>Gets the runs of the line being gathered, sorted by x.</summary>
    internal List<int> Line { get; } = [];

    /// <summary>Gets the bidi segments of the line being closed.</summary>
    internal List<TextSegment> Segments { get; } = [];

    /// <summary>Gets the code each font shows a space with, or -1.</summary>
    internal Dictionary<PdfFont, int> SpaceCodes { get; } = [];

    /// <summary>Gets the runs collected by <see cref="Device"/>.</summary>
    internal List<TextRun> Runs { get; }

    /// <summary>Gets the glyphs collected by <see cref="Device"/>.</summary>
    internal List<TextGlyph> Glyphs { get; }

    /// <summary>Gets the Unicode characters collected by <see cref="Device"/>.</summary>
    internal List<char> Unicode { get; }

    /// <summary>Gets or sets the page's user-to-viewer matrix.</summary>
    internal ref Matrix3x2 Display => ref _display;

    /// <summary>Gets or sets the page width after rotation.</summary>
    internal ref float PageWidth => ref _pageWidth;

    /// <summary>Gets or sets the page height after rotation.</summary>
    internal ref float PageHeight => ref _pageHeight;

    /// <summary>Gets or sets whether the document asks for right-to-left reading order.</summary>
    internal ref bool RightToLeft => ref _rightToLeft;

    /// <summary>Gets or sets the run whose characters were added last, or -1.</summary>
    internal ref int Previous => ref _previous;

    /// <summary>Gets or sets the direction most lines on the page flow in.</summary>
    internal ref TextOrientation LineDirection => ref _lineDirection;

    /// <summary>Gets or sets the bounds of the current line's runs.</summary>
    internal ref PdfRectangle LineRect => ref _lineRect;

    /// <summary>Gets the device that collects the page's text runs.</summary>
    internal TextDevice Device { get; } = new();
}
