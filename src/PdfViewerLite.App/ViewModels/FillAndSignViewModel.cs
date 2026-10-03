// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.App.Services;
using PdfViewerLite.Core.Geometry;
using ReactiveUI;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.ViewModels;

/// <summary>
/// Fill &amp; Sign for one tab: filling form fields happens directly on the page; signing places a drawn or typed
/// signature where the user clicks. The typed name is remembered for next time.
/// </summary>
[DebuggerDisplay("Fill & Sign: {IsActive}")]
public sealed class FillAndSignViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The services, for the remembered signature name.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="FillAndSignViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The services.</param>
    public FillAndSignViewModel(DocumentTabViewModel owner, AppServices services)
    {
        _owner = owner;
        _services = services;
        StartCommand = ReactiveCommand.Create(Start);
        DoneCommand = ReactiveCommand.Create(Done);
        DrawSignatureCommand = ReactiveCommand.Create(() => Annotations.Tool = AnnotationTool.DrawSignature);
        TypeSignatureCommand = ReactiveCommand.CreateFromTask(TypeSignatureAsync);
    }

    /// <summary>Gets or sets a value indicating whether the Fill &amp; Sign tool row is shown.</summary>
    public bool IsActive
    {
        get;
        set => this.RaiseAndSetIfChanged(ref field, value);
    }

    /// <summary>Gets the remembered typed signature, or an empty string.</summary>
    public string SignatureName => _services.Settings.SignatureName;

    /// <summary>Gets the command showing the Fill &amp; Sign tools.</summary>
    public ReactiveCommand<RxVoid, RxVoid> StartCommand { get; }

    /// <summary>Gets the command hiding the Fill &amp; Sign tools.</summary>
    public ReactiveCommand<RxVoid, RxVoid> DoneCommand { get; }

    /// <summary>Gets the command arming the draw signature tool: drag on the page to sign.</summary>
    public ReactiveCommand<RxVoid, AnnotationTool> DrawSignatureCommand { get; }

    /// <summary>Gets the command asking for (or reusing) a typed signature and arming placement: click on the page to sign.</summary>
    public ReactiveCommand<RxVoid, RxVoid> TypeSignatureCommand { get; }

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
    private void Start()
    {
        Annotations.IsAnnotating = false;
        Annotations.Tool = AnnotationTool.Select;
        IsActive = true;
    }

    /// <summary>Hides the tools.</summary>
    private void Done()
    {
        Annotations.Tool = AnnotationTool.Select;
        IsActive = false;
    }

    /// <summary>Asks for the name to sign with, offering the remembered one, then arms placement.</summary>
    /// <returns>A task.</returns>
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
