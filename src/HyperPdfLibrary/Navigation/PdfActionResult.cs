// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Navigation;

/// <summary>What happened when an action, and the actions that follow it, were run.</summary>
[Flags]
public enum PdfActionResult
{
    /// <summary>Nothing ran.</summary>
    None = 0,

    /// <summary>At least one action ran.</summary>
    Ran = 1 << 0,

    /// <summary>An action's type is not run by the library or the host (for example JavaScript), so it was skipped.</summary>
    Skipped = 1 << 1,

    /// <summary>The host declined an action, for example because the user said no.</summary>
    Declined = 1 << 2,

    /// <summary>An action changed the document's objects (field values or hidden fields), so it has unsaved edits and pages need drawing again.</summary>
    Changed = 1 << 3,

    /// <summary>An action showed or hid layers, which changes how pages draw but not the file.</summary>
    LayersChanged = 1 << 4,
}
