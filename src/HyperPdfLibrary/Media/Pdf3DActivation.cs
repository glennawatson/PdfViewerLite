// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Media;

/// <summary>When and how a 3D annotation activates (<c>/3DA</c>).</summary>
/// <param name="ActivateOn">The activation event: PO (page opened), PV (page visible) or XA (explicit).</param>
/// <param name="DeactivateOn">The deactivation event: PC (page closed), PI (page invisible) or XD (explicit).</param>
/// <param name="ActivationStyle">The activation instantiation (<c>/AIS</c>): I (live) or U (uninstantiated).</param>
/// <param name="DeactivationStyle">The deactivation instantiation (<c>/DIS</c>): U, I or L.</param>
/// <param name="ShowToolbar">Whether the toolbar shows (<c>/TB</c>).</param>
/// <param name="ShowNavigator">Whether the navigator panel shows (<c>/NP</c>).</param>
[DebuggerDisplay("Pdf3DActivation: {ActivateOn}/{DeactivateOn}")]
public sealed record Pdf3DActivation(string ActivateOn, string DeactivateOn, string ActivationStyle, string DeactivationStyle, bool ShowToolbar, bool ShowNavigator);
