// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Text;

/// <summary>How a run's /ActualText marked content affects it.</summary>
internal enum ActualTextState
{
    /// <summary>The run has no /ActualText; its glyphs give its text.</summary>
    Pass = 0,

    /// <summary>The run's /ActualText was already used by an earlier run, or has nothing printable; the run adds nothing.</summary>
    Done = 1,

    /// <summary>The run's /ActualText replaces its glyphs.</summary>
    Replace = 2,
}
