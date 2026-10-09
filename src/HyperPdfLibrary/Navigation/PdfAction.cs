// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Navigation;

/// <summary>
/// An action from a link, bookmark, form field or trigger: exactly one of the action records, or no action when
/// <c>Value</c> is <see langword="null"/>. The form, media, 3D, layer, thread, transition and document part cases are data
/// only (nothing here runs), and <see cref="UnsupportedAction"/> holds any other <c>/S</c> name. Every case is a reference
/// type, so holding one never boxes, and a <c>switch</c> over the cases is checked for exhaustiveness.
/// </summary>
[DebuggerDisplay("PdfAction: {Value}")]
public union PdfAction(
    GoToAction,
    UriAction,
    RemoteGoToAction,
    LaunchAction,
    EmbeddedGoToAction,
    NamedAction,
    JavaScriptAction,
    SubmitFormAction,
    ResetFormAction,
    ImportDataAction,
    HideAction,
    SoundAction,
    MovieAction,
    RenditionAction,
    TransAction,
    GoTo3DViewAction,
    RichMediaExecuteAction,
    SetOcgStateAction,
    ThreadAction,
    GoToDpAction,
    UnsupportedAction) : IEquatable<PdfAction>
{
    /// <summary>Gets a value indicating whether there is an action.</summary>
    public bool HasAction => Value is not null;

    /// <summary>Gets a value indicating whether this is one of the viewer-facing kinds: go to, URI, remote go to, launch, embedded go to, named or JavaScript.</summary>
    public bool IsNavigation => Value is GoToAction or UriAction or RemoteGoToAction or LaunchAction or EmbeddedGoToAction or NamedAction or JavaScriptAction;

    /// <summary>Determines whether two actions are equal.</summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    /// <returns><see langword="true"/> when equal.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator ==(PdfAction left, PdfAction right) => left.Equals(right);

    /// <summary>Determines whether two actions differ.</summary>
    /// <param name="left">The first action.</param>
    /// <param name="right">The second action.</param>
    /// <returns><see langword="true"/> when different.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool operator !=(PdfAction left, PdfAction right) => !left.Equals(right);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool Equals(PdfAction other) => Equals(Value, other.Value);

    /// <inheritdoc/>
    public override bool Equals(object? obj) => obj is PdfAction other && Equals(other);

    /// <inheritdoc/>
    public override int GetHashCode() => Value?.GetHashCode() ?? 0;
}
