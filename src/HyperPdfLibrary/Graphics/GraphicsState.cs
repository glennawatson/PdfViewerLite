// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Fonts;

namespace HyperPdfLibrary.Graphics;

/// <summary>
/// The graphics state of PDF 32000 §8.4, held by value so <c>q</c> saves it with one copy onto a reused stack. Colours
/// are already resolved to RGB.
/// </summary>
[DebuggerDisplay("GraphicsState: width {LineWidth}")]
internal record struct GraphicsState
{
    /// <summary>The default miter limit.</summary>
    private const float DefaultMiterLimit = 10;

    /// <summary>Initializes a new instance of the <see cref="GraphicsState"/> struct with the initial values.</summary>
    public GraphicsState()
    {
        Ctm = Matrix3x2.Identity;
        Fill = ColorState.Black;
        Stroke = ColorState.Black;
        LineWidth = 1;
        MiterLimit = DefaultMiterLimit;
        FillAlpha = 1;
        StrokeAlpha = 1;
        HorizontalScaling = 1;
        Dash = [];
        TextKnockout = true;
    }

    /// <summary>Gets or sets the current transformation matrix, from user space to the page.</summary>
    internal Matrix3x2 Ctm { get; set; }

    /// <summary>Gets or sets the fill colour.</summary>
    internal ColorState Fill { get; set; }

    /// <summary>Gets or sets the stroke colour.</summary>
    internal ColorState Stroke { get; set; }

    /// <summary>Gets or sets the line width in user space units; 0 means the thinnest line the device can draw.</summary>
    internal float LineWidth { get; set; }

    /// <summary>Gets or sets the line cap: 0 butt, 1 round, 2 projecting square.</summary>
    internal int LineCap { get; set; }

    /// <summary>Gets or sets the line join: 0 miter, 1 round, 2 bevel.</summary>
    internal int LineJoin { get; set; }

    /// <summary>Gets or sets the miter limit.</summary>
    internal float MiterLimit { get; set; }

    /// <summary>Gets or sets the dash lengths; empty for solid lines.</summary>
    internal ImmutableArray<float> Dash { get; set; }

    /// <summary>Gets or sets the dash phase.</summary>
    internal float DashPhase { get; set; }

    /// <summary>Gets or sets the fill (non-stroking) alpha, /ca.</summary>
    internal float FillAlpha { get; set; }

    /// <summary>Gets or sets the stroke alpha, /CA.</summary>
    internal float StrokeAlpha { get; set; }

    /// <summary>Gets or sets the blend mode.</summary>
    internal PdfBlendMode BlendMode { get; set; }

    /// <summary>Gets or sets the soft mask, or null for none.</summary>
    internal PdfSoftMask? SoftMask { get; set; }

    /// <summary>Gets or sets the character spacing, Tc.</summary>
    internal float CharacterSpacing { get; set; }

    /// <summary>Gets or sets the word spacing, Tw.</summary>
    internal float WordSpacing { get; set; }

    /// <summary>Gets or sets the horizontal scaling as a fraction (Tz divided by 100).</summary>
    internal float HorizontalScaling { get; set; }

    /// <summary>Gets or sets the leading, TL.</summary>
    internal float Leading { get; set; }

    /// <summary>Gets or sets the font, or null before Tf.</summary>
    internal PdfFont? Font { get; set; }

    /// <summary>Gets or sets the font size.</summary>
    internal float FontSize { get; set; }

    /// <summary>Gets or sets the text render mode, Tr: 0 fill, 1 stroke, 2 both, 3 invisible, 4-7 the same plus clip.</summary>
    internal int RenderMode { get; set; }

    /// <summary>Gets or sets the text rise, Ts.</summary>
    internal float Rise { get; set; }

    /// <summary>Gets or sets a value indicating whether stroking overprints, /OP.</summary>
    internal bool StrokeOverprint { get; set; }

    /// <summary>Gets or sets a value indicating whether filling and images overprint, /op (or /OP when /op is absent).</summary>
    internal bool FillOverprint { get; set; }

    /// <summary>Gets or sets the overprint mode, /OPM: 0 or 1.</summary>
    internal int OverprintMode { get; set; }

    /// <summary>Gets or sets a value indicating whether the glyphs of one text show knock each other out, /TK.</summary>
    internal bool TextKnockout { get; set; }
}
