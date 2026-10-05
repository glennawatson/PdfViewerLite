// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Fill &amp; Sign for one tab: filling form fields happens directly on the page; signing places a drawn or typed
/// signature where the user clicks. The typed name is remembered for next time.
/// </summary>
[DebuggerDisplay("Fill & Sign: {IsActive}")]
public sealed partial class FillAndSignViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The services, for the remembered signature name.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="FillAndSignViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The services.</param>
    public FillAndSignViewModel(DocumentTabViewModel owner, AppServices services) => (_owner, _services) = (owner, services);

    /// <summary>Gets or sets a value indicating whether the Fill &amp; Sign tool row is shown.</summary>
    [Reactive]
    public partial bool IsActive { get; set; }

    /// <summary>Gets the remembered typed signature, or an empty string.</summary>
    public string SignatureName => _services.Settings.SignatureName;

    /// <summary>Gets the tab's annotation state, which holds the active tool.</summary>
    private AnnotationsViewModel Annotations => _owner.Annotations;

    /// <summary>Places the typed signature at a point and goes back to filling.</summary>
    /// <param name="page">The page.</param>
    /// <param name="location">The top-left corner of the signature.</param>
    public void PlaceSignature(int page, PagePoint location)
    {
        if (Annotations.PlaceTypedSignature(page, location, SignatureName))
        {
            Annotations.Tool = AnnotationTool.Select;
        }
    }

    /// <summary>Shows the tools; annotation tools are put away so only one tool row is ever shown.</summary>
    [ReactiveCommand]
    private void Start()
    {
        Annotations.IsAnnotating = false;
        Annotations.Tool = AnnotationTool.Select;
        IsActive = true;
    }

    /// <summary>Hides the tools.</summary>
    [ReactiveCommand]
    private void Done()
    {
        Annotations.Tool = AnnotationTool.Select;
        IsActive = false;
    }

    /// <summary>Arms the draw signature tool: drag on the page to sign.</summary>
    /// <returns>The armed tool.</returns>
    [ReactiveCommand]
    private AnnotationTool DrawSignature() => Annotations.Tool = AnnotationTool.DrawSignature;

    /// <summary>Asks for the name to sign with, offering the remembered one, then arms placement.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task TypeSignatureAsync()
    {
        var name = await Annotations.PromptInteraction.Handle(new("Type Signature", "Your name, as you sign it", SignatureName, "Use Signature", false)).ToTask().ConfigureAwait(true);
        if (string.IsNullOrWhiteSpace(name))
        {
            return;
        }

        _services.Settings.SignatureName = name.Trim();
        _services.SaveSettings();
        this.RaisePropertyChanged(nameof(SignatureName));
        Annotations.Tool = AnnotationTool.PlaceSignature;
    }
}
